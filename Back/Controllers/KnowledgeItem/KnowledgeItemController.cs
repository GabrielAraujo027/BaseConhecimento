using BaseConhecimento.Data;
using BaseConhecimento.DTOs.Chamados.Requests;
using BaseConhecimento.DTOs.Chat;
using BaseConhecimento.DTOs.Knowledge;
using BaseConhecimento.DTOs.Knowledge.Requests;
using BaseConhecimento.Models.Chamados;
using BaseConhecimento.Models.Chamados.Enums;
using BaseConhecimento.Models.Knowledge;
using BaseConhecimento.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseConhecimento.Controllers;

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
    }

    // -------- Ingest (1) --------
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] IngestKnowledgeDTO dto, CancellationToken ct)
    {
        if (!Validar(dto, out var erro)) return BadRequest(erro);

        var item = new KnowledgeItem
        {
            Categoria = dto.Categoria,
            Subcategoria = dto.Subcategoria,
            Conteudo = dto.Conteudo
        };

        var textoCompleto = $"{dto.Conteudo} {dto.PerguntasFrequentes}";
        var emb = await _emb.CreateEmbeddingAsync(textoCompleto, ct);
        item.SetEmbedding(emb);

        _ctx.KnowledgeBase.Add(item);
        await _ctx.SaveChangesAsync(ct);

        return Ok(new { message = "Artigo salvo na base de conhecimento", id = item.Id });
    }

    // -------- Ingest (batch) --------
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
                Conteudo = dto.Conteudo
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

    [HttpPost("chat")]
    public async Task<ActionResult<ChatKnowledgeResponseDTO>> Chat(
    [FromBody] ChatKnowledgeRequestDTO req,
    CancellationToken ct)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { error = "Message é obrigatório" });

        // 1) Gerar embedding da pergunta do usuário
        var qEmb = await _emb.CreateEmbeddingAsync(req.Message, ct);

        // 2) Trazer todos os artigos (conteúdo + perguntas frequentes contam para embedding)
        var items = await _ctx.KnowledgeBase.AsNoTracking().ToListAsync(ct);
        if (items.Count == 0)
            return await AbrirChamadoEDevolver(req.Message);

        var ranked = items
            .Select(i =>
            {
                // Combinar conteúdo + perguntas frequentes para enriquecer similaridade
                var textoCompleto = $"{i.Conteudo} {i.PerguntasFrequentes}";
                var embItem = i.GetEmbedding();
                var score = CosineSimilarity(qEmb, embItem);
                return new ScoredItem { Item = i, Score = score };
            })
            .OrderByDescending(x => x.Score)
            .Take(1) // só o top-1
            .ToList();

        var highThreshold = 0.70f; // ajustável

        if (ranked.Any() && ranked[0].Score >= highThreshold)
        {
            var artigo = ranked[0].Item;

            var contexto =
                $@"O usuário perguntou: ""{req.Message}"".
            Base de conhecimento encontrada:
            Categoria: {artigo.Categoria}
            Subcategoria: {artigo.Subcategoria}
            Conteúdo: {artigo.Conteudo}

            Gere uma resposta clara, objetiva, amigável, com tom humano, sem repetir literalmente o artigo.
            Explique em até 5 linhas.";

            var reply = await _llama.GenerateAsync(contexto, ct);

            return Ok(new ChatKnowledgeResponseDTO
            {
                Reply = reply
            });
        }

        // 4) Caso não encontre nada relevante → cria chamado
        return await AbrirChamadoEDevolver(req.Message);
    }


    // -------- Helpers --------
    private static bool Validar(IngestKnowledgeDTO dto, out string erro)
    {
        if (dto is null) { erro = "Payload inválido."; return false; }
        if (string.IsNullOrWhiteSpace(dto.Categoria)) { erro = "Categoria é obrigatória."; return false; }
        if (string.IsNullOrWhiteSpace(dto.Subcategoria)) { erro = "Subcategoria é obrigatória."; return false; }
        if (string.IsNullOrWhiteSpace(dto.Conteudo)) { erro = "Conteúdo é obrigatório."; return false; }
        erro = ""; return true;
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

    private async Task<int> CriarChamadoAutomatico(CriarChamadoDTO request)
    {
        try
        {
            var chamado = new Chamado
            {
                Titulo = request.Titulo?.Trim() ?? "[BOT] Chamado",
                Descricao = request.Descricao?.Trim() ?? "",
                StatusEnum = StatusChamadoEnum.Pendente,
                SetorResponsavel = "TI"
            };
            _ctx.Chamados.Add(chamado);
            await _ctx.SaveChangesAsync();
            return chamado.Id;
        }
        catch { return 0; }
    }
}
