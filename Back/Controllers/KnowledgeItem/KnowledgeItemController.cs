using BaseConhecimento.Data;
using BaseConhecimento.DTOs.Chamados.Requests;
using BaseConhecimento.DTOs.Chat.Requests;
using BaseConhecimento.DTOs.Knowledge;
using BaseConhecimento.DTOs.Knowledge.Requests;
using BaseConhecimento.Models.Chamados;
using BaseConhecimento.Models.Chamados.Enums;
using BaseConhecimento.Models.Knowledge;
using BaseConhecimento.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BaseConhecimento.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KnowledgeController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        private readonly IEmbeddingService _emb;
        private readonly ILlamaService _llama;

        public KnowledgeController(AppDbContext ctx, IEmbeddingService emb, ILlamaService llama)
        {
            _ctx = ctx;
            _emb = emb;
            _llama = llama;
        }

        private sealed class ScoredItem
        {
            public required KnowledgeItem Item { get; init; }
            public float Score { get; init; }
            public float Cos { get; init; }
            public float Lex { get; init; }
        }

        private string? GetCurrentUserEmail()
            => User?.FindFirstValue(ClaimTypes.Email)
               ?? User?.Claims.FirstOrDefault(c => c.Type.Equals("email", StringComparison.OrdinalIgnoreCase))?.Value;

        [HttpPost("ingest")]
        [Authorize(Roles = "Atendente")]
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

            var textoCompleto = $"{dto.Conteudo} {dto.PerguntasFrequentes}";
            var emb = await _emb.CreateEmbeddingAsync(textoCompleto, ct);
            item.SetEmbedding(emb);

            _ctx.KnowledgeBase.Add(item);
            await _ctx.SaveChangesAsync(ct);

            return Ok(new { message = "Artigo salvo na base de conhecimento", id = item.Id });
        }

        [HttpPost("ingest/batch")]
        [Authorize(Roles = "Atendente")]
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

        [AllowAnonymous]
        [HttpPost("chat")]
        public async Task<ActionResult<ChatKnowledgeResponseDTO>> Chat(
            [FromBody] ChatKnowledgeRequestDTO req,
            CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Message))
                return BadRequest(new { error = "Message é obrigatório" });

            var expanded = ExpandQuery(req.Message);

            var qEmb = await _emb.CreateEmbeddingAsync(expanded, ct);

            var items = await _ctx.KnowledgeBase.AsNoTracking().ToListAsync(ct);
            if (items.Count == 0)
            {
                var llamaDecWhenEmpty = await TryLlamaHandOffAsync(req, null, ct);
                if (llamaDecWhenEmpty.HandOff)
                {
                    var setor = NormalizeSector(llamaDecWhenEmpty.Sector) ?? "TI";
                    var title = string.IsNullOrWhiteSpace(llamaDecWhenEmpty.Title) ? "Solicitação de suporte" : llamaDecWhenEmpty.Title!.Trim();
                    var ticketId = await CriarChamadoAutomatico(new CriarChamadoDTO
                    {
                        Titulo = title,
                        Descricao = $"Pergunta do usuário (base vazia): {req.Message}"
                    }, setor);
                    var reply = $"Chamado aberto para {title} - #{ticketId} (Suporte - {setor})";
                    return Ok(new ChatKnowledgeResponseDTO { Reply = reply, TicketId = ticketId });
                }

                return Ok(new ChatKnowledgeResponseDTO
                {
                    Reply = "Não encontrei conteúdo na base. Posso abrir um chamado para o setor responsável, se preferir."
                });
            }

            const int TOP_K = 6;
            var prelim = items
                .Select(i => new
                {
                    Item = i,
                    Cos = CosineSimilarity(qEmb, i.GetEmbedding())
                })
                .OrderByDescending(x => x.Cos)
                .Take(TOP_K)
                .ToList();

            var fused = prelim.Select(x =>
            {
                var text = $"{x.Item.Conteudo} {x.Item.PerguntasFrequentes}";
                var lex = LexicalOverlap(expanded, text);
                var score = 0.7f * x.Cos + 0.3f * lex;
                return new ScoredItem { Item = x.Item, Cos = x.Cos, Lex = lex, Score = score };
            })
            .OrderByDescending(x => x.Score)
            .ToList();

            float top1 = fused[0].Score;
            float top2 = fused.Count > 1 ? fused[1].Score : 0f;
            bool confident = (top1 >= 0.76f) || (top1 >= 0.64f && (top1 - top2) >= 0.10f);

            var artigo = fused[0].Item;

            LlamaDecision llamaDecision = confident
                ? new LlamaDecision(false, null, null)
                : await TryLlamaHandOffAsync(req, artigo, ct);

            if (!confident && llamaDecision.HandOff)
            {
                var setor = NormalizeSector(llamaDecision.Sector) ?? SectorFromCategory(artigo?.Categoria) ?? "TI";
                var title = !string.IsNullOrWhiteSpace(llamaDecision.Title)
                    ? llamaDecision.Title!.Trim()
                    : InferTicketTitle(req.Message, artigo);

                var ticketId = await CriarChamadoAutomatico(new CriarChamadoDTO
                {
                    Titulo = title,
                    Descricao = BuildTicketDescription(req, artigo)
                }, setor);

                var reply = $"Chamado aberto para {title} - #{ticketId} (Suporte - {setor})";
                return Ok(new ChatKnowledgeResponseDTO { Reply = reply, TicketId = ticketId });
            }

            if (confident && artigo is not null)
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

            return Ok(new ChatKnowledgeResponseDTO
            {
                Reply = "Não encontrei uma correspondência clara na base. Posso abrir um chamado para o setor responsável, se preferir."
            });
        }

        private sealed record LlamaDecision(bool HandOff, string? Sector, string? Title);

        private async Task<LlamaDecision> TryLlamaHandOffAsync(
            ChatKnowledgeRequestDTO req,
            KnowledgeItem? artigo,
            CancellationToken ct)
        {
            try
            {
                var hist = (req.History ?? new List<ChatMessageDTO>())
                    .Select(h => new { role = h.Role ?? "user", content = h.Content ?? "" })
                    .Where(h => !string.IsNullOrWhiteSpace(h.content))
                    .TakeLast(8)
                    .ToList();

                var artigoHint = artigo is null ? "N/A" : $"{artigo.Categoria} / {artigo.Subcategoria}";

                var prompt =
                    $@"
                    Você é um classificador. Dado o diálogo, responda em JSON estrito:
                    {{
                      ""handoff"": true|false,        // true se o usuário quer que um humano execute/assuma
                      ""sector"": ""TI|Facilities|RH|Financeiro|Compras|Segurança da Informação|Logística|Produção|Processos|Data & Analytics|Qualidade|Jurídico|Marketing|Comercial|Infra & DevOps|Atendimento Humano|"" ou """",
                      ""title"": ""título curto do chamado""
                    }}
                    Histórico:
                    {string.Join("\n", hist.Select(h => $"{h.role}: {h.content}"))}
                    Mensagem atual:
                    {req.Message}
                    Artigo relacionado: {artigoHint}
                    Saída apenas JSON válido.
                    ";

                var raw = await _llama.GenerateAsync(prompt, ct);
                var json = ExtractJson(raw);
                if (string.IsNullOrWhiteSpace(json)) return new LlamaDecision(false, null, null);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var handoff = root.TryGetProperty("handoff", out var hEl) && hEl.ValueKind == JsonValueKind.True;
                string? sector = root.TryGetProperty("sector", out var sEl) && sEl.ValueKind == JsonValueKind.String ? sEl.GetString() : null;
                string? title = root.TryGetProperty("title", out var tEl) && tEl.ValueKind == JsonValueKind.String ? tEl.GetString() : null;

                return new LlamaDecision(handoff, sector, title);
            }
            catch
            {
                return new LlamaDecision(false, null, null);
            }
        }

        private static string? ExtractJson(string raw)
        {
            var m = Regex.Match(raw, @"\{[\s\S]*\}");
            return m.Success ? m.Value : null;
        }

        private static bool Validar(IngestKnowledgeDTO dto, out string erro)
        {
            if (dto is null) { erro = "Payload inválido."; return false; }
            if (string.IsNullOrWhiteSpace(dto.Categoria)) { erro = "Categoria é obrigatória."; return false; }
            if (string.IsNullOrWhiteSpace(dto.Subcategoria)) { erro = "Subcategoria é obrigatória."; return false; }
            if (string.IsNullOrWhiteSpace(dto.Conteudo)) { erro = "Conteúdo é obrigatório."; return false; }
            erro = ""; return true;
        }

        private static IEnumerable<string> Tok(string s) =>
            Regex.Matches(s ?? "", @"[A-Za-zÀ-ÿ0-9]+")
                 .Select(m => m.Value.ToLowerInvariant())
                 .Where(w => w.Length > 2);

        private static float LexicalOverlap(string query, string text)
        {
            var qs = Tok(query).ToHashSet();
            var ts = Tok(text).ToHashSet();
            if (qs.Count == 0 || ts.Count == 0) return 0f;
            var inter = qs.Intersect(ts).Count();
            return (float)inter / Math.Max(1, qs.Count);
        }

        private static readonly (string key, string[] syn)[] Synonyms = new[]
        {
            ("vale refeicao", new[] {"vale-refeicao","vr","ticket refeição","ticket alimentacao","auxilio alimentacao"}),
            ("cartucho", new[] {"tinta","toner","impressora sem tinta","substituir cartucho"}),
            ("atestado", new[] {"comprovante medico","licenca medica","laudo","afastamento"}),
            ("senha", new[] {"reset de senha","redefinir senha","trocar senha","recuperar acesso"}),
            ("reembolso", new[] {"ressarcimento","adiantamento de despesas","prestacao de contas"}),
            ("internet", new[] {"rede","wifi","conexao","link"}),
            ("ferias", new[] {"periodo de descanso","abono ferias","agendar ferias"}),
            ("nota fiscal", new[] {"nf-e","nfe","fatura","invoice"}),
        };

        private static string ExpandQuery(string q)
        {
            var t = " " + q.ToLowerInvariant() + " ";
            var extra = new List<string>();
            foreach (var (key, syns) in Synonyms)
            {
                if (t.Contains(" " + key + " "))
                    extra.AddRange(syns);
            }
            return extra.Count == 0 ? q : q + " " + string.Join(' ', extra);
        }

        private static readonly string[] Openers = {
            "Encontrei algo que pode te ajudar:",
            "Beleza — aqui vai o passo a passo:",
            "Claro! Olha como resolver:",
            "Seguem as instruções objetivas:",
            "Achei um guia que resolve isso:"
        };
        private static readonly string[] Closers = {
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

        private static float CosineSimilarity(float[] v1, float[] v2)
        {
            if (v1.Length == 0 || v2.Length == 0 || v1.Length != v2.Length) return 0f;
            float dot = 0, a = 0, b = 0;
            for (int i = 0; i < v1.Length; i++) { dot += v1[i] * v2[i]; a += v1[i] * v1[i]; b += v2[i] * v2[i]; }
            return dot / ((float)Math.Sqrt(a) * (float)Math.Sqrt(b) + 1e-6f);
        }

        private static string BuildTicketDescription(ChatKnowledgeRequestDTO req, KnowledgeItem? artigo)
        {
            var hist = (req.History ?? new List<ChatMessageDTO>())
                .Select(h => $"- {h.Role}: {h.Content}")
                .ToList();

            return
            $@"Solicitação aberta automaticamente pelo assistente.

            Histórico recente:
            {string.Join("\n", hist.TakeLast(8))}

            Mensagem do usuário: {req.Message}
            Artigo relacionado: {(artigo is null ? "N/A" : $"{artigo.Categoria} / {artigo.Subcategoria}")}";
        }

        private static string InferTicketTitle(string userMsg, KnowledgeItem? artigo)
        {
            var t = userMsg.ToLowerInvariant();
            if (t.Contains("cartucho") || t.Contains("impressora"))
                return "Troca de cartucho de impressora";
            if (artigo != null && !string.IsNullOrWhiteSpace(artigo.Subcategoria))
                return $"Solicitação: {artigo.Subcategoria}";
            return "Solicitação de suporte";
        }

        private static string? NormalizeSector(string? sector)
        {
            if (string.IsNullOrWhiteSpace(sector)) return null;
            var s = sector.Trim().ToLowerInvariant();

            if (s.Contains("ti")) return "TI";
            if (s.StartsWith("seguran")) return "Segurança da Informação";
            if (s.StartsWith("finan")) return "Financeiro";
            if (s.StartsWith("rec") || s.StartsWith("rh")) return "RH";
            if (s.StartsWith("compr")) return "Compras";
            if (s.StartsWith("jur")) return "Jurídico";
            if (s.StartsWith("facil") || s.Contains("predial")) return "Facilities";
            if (s.StartsWith("log")) return "Logística";
            if (s.StartsWith("prod")) return "Produção";
            if (s.StartsWith("proc") || s.Contains("pmo")) return "Processos";
            if (s.Contains("data") || s.Contains("analyt")) return "Data & Analytics";
            if (s.StartsWith("qual")) return "Qualidade";
            if (s.StartsWith("mark")) return "Marketing";
            if (s.StartsWith("comer")) return "Comercial";
            if (s.Contains("infra")) return "Infra & DevOps";
            if (s.Contains("atendimento")) return "Atendimento Humano";
            return char.ToUpper(s[0]) + s[1..];
        }

        private static string? SectorFromCategory(string? categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria)) return null;
            var c = categoria.Trim().ToLowerInvariant();

            if (c.Contains("ti")) return "TI";
            if (c.Contains("facil")) return "Facilities";
            if (c.Contains("recursos humanos") || c == "rh") return "RH";
            if (c.Contains("finance")) return "Financeiro";
            if (c.Contains("compras")) return "Compras";
            if (c.Contains("segurança")) return "Segurança da Informação";
            if (c.Contains("logíst")) return "Logística";
            if (c.Contains("produ")) return "Produção";
            if (c.Contains("process")) return "Processos";
            if (c.Contains("data") || c.Contains("analytics")) return "Data & Analytics";
            if (c.Contains("qualid")) return "Qualidade";
            if (c.Contains("juríd")) return "Jurídico";
            if (c.Contains("marketing")) return "Marketing";
            if (c.Contains("comercial")) return "Comercial";
            if (c.Contains("infra")) return "Infra & DevOps";
            if (c.Contains("atendimento")) return "Atendimento Humano";
            return null;
        }

        private async Task<int> CriarChamadoAutomatico(CriarChamadoDTO request, string setor)
        {
            try
            {
                var chamado = new Chamado
                {
                    Titulo = request.Titulo?.Trim() ?? "[BOT] Chamado",
                    Descricao = request.Descricao?.Trim() ?? "",
                    StatusEnum = StatusChamadoEnum.Pendente,
                    SetorResponsavel = string.IsNullOrWhiteSpace(setor) ? "TI" : setor.Trim(),
                    DataCriacao = DateTime.UtcNow,
                    Solicitante = GetCurrentUserEmail() ?? "BOT"
                };
                _ctx.Chamados.Add(chamado);
                await _ctx.SaveChangesAsync();
                return chamado.Id;
            }
            catch { return 0; }
        }
    }
}
