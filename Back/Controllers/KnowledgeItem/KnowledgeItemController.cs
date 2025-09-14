using BaseConhecimento.Data;
using BaseConhecimento.DTOs.Chamados.Requests;
using BaseConhecimento.DTOs.Chat;
using BaseConhecimento.DTOs.Knowledge;
using BaseConhecimento.DTOs.Knowledge.Requests;
using BaseConhecimento.Models.Chamados;
using BaseConhecimento.Models.Chamados.Enums;
using BaseConhecimento.Models.Knowledge;
using BaseConhecimento.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseConhecimento.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KnowledgeController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        private readonly IEmbeddingService _emb;

        public KnowledgeController(AppDbContext ctx, IEmbeddingService emb)
        {
            _ctx = ctx;
            _emb = emb;
        }

        private sealed class ScoredItem
        {
            public required KnowledgeItem Item { get; init; }
            public float Score { get; init; }
        }

        // ---------------- Ingest (1) ----------------
        [HttpPost("ingest")]
        public async Task<IActionResult> Ingest([FromBody] IngestKnowledgeDTO dto, CancellationToken ct)
        {
            if (!Validar(dto, out var erro)) return BadRequest(erro);

            var item = new KnowledgeItem
            {
                Categoria = dto.Categoria,
                Subcategoria = dto.Subcategoria,
                Conteudo = dto.Conteudo,
                PerguntasFrequentes = dto.PerguntasFrequentes ?? string.Empty
            };

            // Embedding do conteúdo + FAQs para ranking melhor
            var textoCompleto = $"{dto.Conteudo} {dto.PerguntasFrequentes}";
            var emb = await _emb.CreateEmbeddingAsync(textoCompleto, ct);
            item.SetEmbedding(emb);

            _ctx.KnowledgeBase.Add(item);
            await _ctx.SaveChangesAsync(ct);

            return Ok(new { message = "Artigo salvo na base de conhecimento", id = item.Id });
        }

        // -------------- Ingest (batch) --------------
        [HttpPost("ingest/batch")]
        public async Task<IActionResult> IngestBatch([FromBody] List<IngestKnowledgeDTO> itens, CancellationToken ct)
        {
            var result = new IngestBatchResultDTO();
            if (itens is null || itens.Count == 0) return BadRequest("Envie um array com pelo menos 1 item.");

            var entidades = new List<KnowledgeItem>();
            foreach (var dto in itens)
            {
                if (!Validar(dto, out var erro)) { result.Erros.Add(erro); continue; }

                var item = new KnowledgeItem
                {
                    Categoria = dto.Categoria,
                    Subcategoria = dto.Subcategoria,
                    Conteudo = dto.Conteudo,
                    PerguntasFrequentes = dto.PerguntasFrequentes ?? string.Empty
                };

                var textoCompleto = $"{dto.Conteudo} {dto.PerguntasFrequentes}";
                var emb = await _emb.CreateEmbeddingAsync(textoCompleto, ct);
                item.SetEmbedding(emb);

                entidades.Add(item);
            }

            if (entidades.Count == 0)
                return BadRequest(new { message = "Nenhum item válido para inserir.", erros = result.Erros });

            _ctx.KnowledgeBase.AddRange(entidades);
            await _ctx.SaveChangesAsync(ct);

            result.Inseridos = entidades.Count;
            result.Ids = entidades.Select(e => e.Id).ToList();
            return Ok(result);
        }

        // -------------- Chat: responde OU abre chamado se usuário pedir --------------
        [AllowAnonymous]
        [HttpPost("chat")]
        public async Task<ActionResult<ChatKnowledgeResponseDTO>> Chat(
            [FromBody] ChatKnowledgeRequestDTO req,
            CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Message))
                return BadRequest(new { error = "Message é obrigatório" });

            // Verifica se o usuário explicitamente quer atendimento humano
            var wantsHandOff = WantsHumanHandOff(req.Message)
                               || (req.History?.Any(h => h.Role == "user" && WantsHumanHandOff(h.Content)) ?? false);

            // Gera embedding da pergunta
            var qEmb = await _emb.CreateEmbeddingAsync(req.Message, ct);

            // Busca artigos e rankeia (top-1)
            var items = await _ctx.KnowledgeBase.AsNoTracking().ToListAsync(ct);
            KnowledgeItem? artigo = null;
            float score = 0f;

            if (items.Count > 0)
            {
                var ranked = items
                    .Select(i =>
                    {
                        var embItem = i.GetEmbedding();
                        var s = CosineSimilarity(qEmb, embItem);
                        return new ScoredItem { Item = i, Score = s };
                    })
                    .OrderByDescending(x => x.Score)
                    .Take(1)
                    .ToList();

                if (ranked.Any())
                {
                    artigo = ranked[0].Item;
                    score = ranked[0].Score;
                }
            }

            // limiar para "resposta confiante"
            var highThreshold = 0.70f;

            // Se o usuário pediu mão para humano, abrimos chamado com base no melhor artigo (se houver)
            if (wantsHandOff)
            {
                var (titulo, descricao, setor) = BuildTicketFromContext(req.Message, artigo);
                var ticketId = await CriarChamadoAutomatico(new CriarChamadoDTO
                {
                    Titulo = titulo,
                    Descricao = descricao
                    // Setor e status são pré-definidos dentro do método
                });

                var setorLabel = string.IsNullOrEmpty(setor) ? "Suporte - TI" : $"Suporte - {setor}";
                var reply = $"Chamado aberto para {titulo} - #{ticketId} ({setorLabel})";
                return Ok(new ChatKnowledgeResponseDTO { Reply = reply, TicketId = ticketId });
            }

            // Caso normal: se achou artigo com confiança, responde dinamicamente
            if (artigo is not null && score >= highThreshold)
            {
                var opener = Pick(Openers, req.Message);
                var closer = Pick(Closers, req.Message);

                var resposta =
                $@"{opener}
                {artigo.Categoria} - {artigo.Subcategoria}
                {artigo.Conteudo}
                {closer}";

                return Ok(new ChatKnowledgeResponseDTO { Reply = resposta });
            }

            // Se não achou nada confiante, abre chamado automaticamente (fallback)
            return await AbrirChamadoEDevolver(req.Message);
        }

        private static bool Validar(IngestKnowledgeDTO dto, out string erro)
        {
            if (dto is null) { erro = "Payload inválido."; return false; }
            if (string.IsNullOrWhiteSpace(dto.Categoria)) { erro = "Categoria é obrigatória."; return false; }
            if (string.IsNullOrWhiteSpace(dto.Subcategoria)) { erro = "Subcategoria é obrigatória."; return false; }
            if (string.IsNullOrWhiteSpace(dto.Conteudo)) { erro = "Conteúdo é obrigatório."; return false; }
            erro = ""; return true;
        }

        private static readonly string[] Openers = new[]
        {
            "Encontrei algo que pode te ajudar:",
            "Beleza — aqui vai o passo a passo:",
            "Claro! Olha como resolver:",
            "Seguem as instruções objetivas:",
            "Achei um guia que resolve isso:"
        };

        private static readonly string[] Closers = new[]
        {
            "Se preferir, abro um chamado com o time responsável.",
            "Caso queira, posso acionar o suporte pra fazer por você.",
            "Quer que eu crie um chamado para o setor responsável?",
            "Posso abrir um ticket para alguém executar pra você.",
            "Se não conseguir, abro um chamado agora mesmo."
        };

        private static string Pick(string[] arr, string seed)
        {
            var h = Math.Abs(seed.GetHashCode());
            return arr[h % arr.Length];
        }

        private static bool WantsHumanHandOff(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var t = text.ToLowerInvariant();

            // Palavras/expressões que indicam escalar para humano
            string[] patterns =
            {
                "abra um chamado", "abrir um chamado", "abre um chamado",
                "abre um ticket", "abrir um ticket", "abra um ticket",
                "preciso que alguém faça", "preciso que o pessoal", "pessoal do ti",
                "façam para mim", "façam pra mim", "alguém vem aqui", "alguém venha",
                "não consigo fazer", "façam isso", "quero suporte", "mandar um técnico",
                "abrir chamado", "abrir ticket"
            };

            return patterns.Any(p => t.Contains(p));
        }

        private static string InferSetor(KnowledgeItem? artigo)
        {
            if (artigo is null) return "TI";
            var cat = (artigo.Categoria ?? "").Trim().ToLowerInvariant();

            if (cat.Contains("ti")) return "TI";
            if (cat.Contains("facility")) return "Facilities";
            if (cat.Contains("facilidades")) return "Facilities";
            if (cat.Contains("rh")) return "RH";
            if (cat.Contains("finance")) return "Financeiro";
            if (cat.Contains("compras")) return "Compras";
            if (cat.Contains("juríd")) return "Jurídico";
            if (cat.Contains("segurança")) return "Segurança";
            return "TI";
        }

        private static (string titulo, string descricao, string setor) BuildTicketFromContext(string userMsg, KnowledgeItem? artigo)
        {
            // Título deduzido pelo tema
            string titulo;
            var msg = userMsg.ToLowerInvariant();

            if (msg.Contains("cartucho") || msg.Contains("impressora"))
                titulo = "Troca de cartucho de impressora";
            else if (artigo != null)
                titulo = $"Solicitação: {artigo.Subcategoria}";
            else
                titulo = "Solicitação de suporte";

            var setor = InferSetor(artigo);

            var descricao =
            $@"Solicitação aberta automaticamente pelo assistente.
            Mensagem do usuário: {userMsg}
            Artigo relacionado: {(artigo is null ? "N/A" : $"{artigo.Categoria} / {artigo.Subcategoria}")}";

            return (titulo, descricao, setor);
        }

        private static float CosineSimilarity(float[] v1, float[] v2)
        {
            if (v1.Length == 0 || v2.Length == 0 || v1.Length != v2.Length) return 0f;
            float dot = 0, a = 0, b = 0;
            for (int i = 0; i < v1.Length; i++) { dot += v1[i] * v2[i]; a += v1[i] * v1[i]; b += v2[i] * v2[i]; }
            return dot / ((float)Math.Sqrt(a) * (float)Math.Sqrt(b) + 1e-6f);
        }

        private async Task<ActionResult<ChatKnowledgeResponseDTO>> AbrirChamadoEDevolver(string pergunta)
        {
            var dtoChamado = new CriarChamadoDTO
            {
                Titulo = "[BOT] Dúvida sem resposta na base",
                Descricao = $"Pergunta do usuário: {pergunta}"
            };

            var id = await CriarChamadoAutomatico(dtoChamado);

            var msg = id > 0
                ? $"Não encontrei resposta na base. Abri automaticamente o chamado #{id}."
                : "Não encontrei resposta na base e não foi possível abrir o chamado automaticamente.";

            return Ok(new ChatKnowledgeResponseDTO { Reply = msg, TicketId = id > 0 ? id : null });
        }

        // Cria chamado direto no banco (status e setor pré-definidos)
        private async Task<int> CriarChamadoAutomatico(CriarChamadoDTO request)
        {
            try
            {
                var chamado = new Chamado
                {
                    Titulo = request.Titulo?.Trim() ?? "[BOT] Chamado",
                    Descricao = request.Descricao?.Trim() ?? "",
                    StatusEnum = StatusChamadoEnum.Pendente,
                    SetorResponsavel = "TI" // pré-definido; ajuste se quiser usar InferSetor()
                };
                _ctx.Chamados.Add(chamado);
                await _ctx.SaveChangesAsync();
                return chamado.Id;
            }
            catch { return 0; }
        }
    }
}
