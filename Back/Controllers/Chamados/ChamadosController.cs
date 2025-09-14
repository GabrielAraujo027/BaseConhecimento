// Controllers/ChamadosController.cs
using BaseConhecimento.Data;
using BaseConhecimento.DTOs.Chamados.Requests;
using BaseConhecimento.DTOs.Chamados.Responses;
using BaseConhecimento.DTOs.Common;
using BaseConhecimento.Models.Chamados;
using BaseConhecimento.Models.Chamados.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseConhecimento.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChamadosController : ControllerBase
{
    private readonly AppDbContext _ctx;
    public ChamadosController(AppDbContext ctx) => _ctx = ctx;

    private string? GetCurrentUserEmail()
        => User?.FindFirstValue(ClaimTypes.Email)
           ?? User?.Claims.FirstOrDefault(c => c.Type.Equals("email", StringComparison.OrdinalIgnoreCase))?.Value;

    // ========================= LISTAGEM (com filtros + paginação) =========================
    // GET /api/chamados?status=Pendente&setor=TI&solicitante=joao@x.com&de=2025-09-01&ate=2025-09-14&search=impressora&page=1&pageSize=20
    [Authorize(Roles = "Atendente")]
    [HttpGet]
    public async Task<ActionResult<ResultadoPaginado<ListarChamadosDTO>>> Listar(
        [FromQuery] StatusChamadoEnum? status,
        [FromQuery] string? setor,
        [FromQuery] string? solicitante,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        const int MAX_PAGE_SIZE = 200;

        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, MAX_PAGE_SIZE);

        IQueryable<Chamado> q = _ctx.Chamados.AsNoTracking();

        if (status.HasValue)
            q = q.Where(c => c.StatusEnum == status.Value);

        if (!string.IsNullOrWhiteSpace(setor))
        {
            var s = setor.Trim().ToLower();
            q = q.Where(c => c.SetorResponsavel.ToLower() == s);
        }

        if (!string.IsNullOrWhiteSpace(solicitante))
        {
            var s = solicitante.Trim();
            q = q.Where(c => c.Solicitante != null && c.Solicitante.Contains(s));
        }

        if (de.HasValue)
        {
            var iniUtc = DateTime.SpecifyKind(de.Value, DateTimeKind.Utc);
            q = q.Where(c => c.DataCriacao >= iniUtc);
        }

        if (ate.HasValue)
        {
            var fimUtc = DateTime.SpecifyKind(ate.Value, DateTimeKind.Utc);
            q = q.Where(c => c.DataCriacao <= fimUtc);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c => c.Titulo.Contains(s) || c.Descricao.Contains(s));
        }

        var totalItems = await q.CountAsync();

        var items = await q
            .OrderByDescending(c => c.DataCriacao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ListarChamadosDTO
            {
                Id = c.Id,
                Titulo = c.Titulo,
                Status = c.StatusEnum,
                SetorResponsavel = c.SetorResponsavel,
                DataCriacao = c.DataCriacao,
                DataConclusao = c.DataConclusao,
                Solicitante = c.Solicitante
            })
            .ToListAsync();

        var result = new ResultadoPaginado<ListarChamadosDTO>
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
            Items = items
        };

        return Ok(result);
    }

    // ========================= DETALHE =========================
    [Authorize(Roles = "Atendente")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Chamado>> GetById(int id)
    {
        var item = await _ctx.Chamados.FindAsync(id);
        return item is null ? NotFound() : item;
    }

    // ========================= CRIAR =========================
    // Qualquer usuário autenticado pode abrir chamado
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Chamado>> Create([FromBody] CriarChamadoDTO request)
    {
        var chamado = new Chamado
        {
            Titulo = request.Titulo?.Trim() ?? "[Chamado]",
            Descricao = request.Descricao?.Trim() ?? "",
            StatusEnum = StatusChamadoEnum.Pendente,
            SetorResponsavel = string.IsNullOrWhiteSpace(request.SetorResponsavel) ? "TI" : request.SetorResponsavel.Trim(),
            DataCriacao = DateTime.UtcNow,
            Solicitante = GetCurrentUserEmail() ?? "Anon"
        };

        _ctx.Chamados.Add(chamado);
        await _ctx.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = chamado.Id }, chamado);
    }

    // ========================= ATUALIZAR =========================
    [Authorize(Roles = "Atendente")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AlterarChamadoDTO request)
    {
        if (id != request.Id) return BadRequest();

        var entity = await _ctx.Chamados.FindAsync(id);
        if (entity is null) return NotFound();

        var statusAnterior = entity.StatusEnum;

        entity.StatusEnum = request.StatusEnum;
        entity.SetorResponsavel = request.SetorResponsavel?.Trim() ?? entity.SetorResponsavel;

        if (statusAnterior != StatusChamadoEnum.Concluido && request.StatusEnum == StatusChamadoEnum.Concluido)
            entity.DataConclusao = DateTime.UtcNow;
        else if (statusAnterior == StatusChamadoEnum.Concluido && request.StatusEnum != StatusChamadoEnum.Concluido)
            entity.DataConclusao = null;

        await _ctx.SaveChangesAsync();
        return Ok(entity);
    }

    // ========================= DELETAR =========================
    [Authorize(Roles = "Atendente")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _ctx.Chamados.FindAsync(id);
        if (entity is null) return NotFound();

        _ctx.Chamados.Remove(entity);
        await _ctx.SaveChangesAsync();
        return NoContent();
    }

    // ========================= RELATÓRIO =========================
    // GET /api/chamados/relatorio?inicio=2025-09-01T00:00:00Z&fim=2025-09-14T23:59:59Z&setor=TI
    [Authorize(Roles = "Atendente")]
    [HttpGet("relatorio")]
    public async Task<ActionResult<RelatorioChamadoDTO>> Relatorio(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] string? setor)
    {
        var now = DateTime.UtcNow;

        IQueryable<Chamado> q = _ctx.Chamados.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(setor))
        {
            var s = setor.Trim().ToLowerInvariant();
            q = q.Where(c => c.SetorResponsavel.ToLower() == s);
        }

        if (inicio.HasValue)
        {
            var iniUtc = DateTime.SpecifyKind(inicio.Value, DateTimeKind.Utc);
            q = q.Where(c => c.DataCriacao >= iniUtc);
        }

        if (fim.HasValue)
        {
            var fimUtc = DateTime.SpecifyKind(fim.Value, DateTimeKind.Utc);
            q = q.Where(c => c.DataCriacao <= fimUtc);
        }

        var umaHoraAtras = now.AddHours(-1);
        var abertosUltimaHora = await q.Where(c => c.DataCriacao >= umaHoraAtras && c.DataCriacao <= now).CountAsync();

        var pendentes = await q.Where(c => c.StatusEnum == StatusChamadoEnum.Pendente).CountAsync();
        var andamento = await q.Where(c => c.StatusEnum == StatusChamadoEnum.EmAndamento).CountAsync();
        var concluidos = await q.Where(c => c.StatusEnum == StatusChamadoEnum.Concluido).CountAsync();
        var cancelados = await q.Where(c => c.StatusEnum == StatusChamadoEnum.Cancelado).CountAsync();

        double? mediaMin = await q
            .Where(c => c.StatusEnum == StatusChamadoEnum.Concluido && c.DataConclusao != null)
            .AverageAsync(c => (double?)EF.Functions.DateDiffMinute(c.DataCriacao, c.DataConclusao!.Value));

        int tempoMedioHoras = mediaMin.HasValue
            ? (int)Math.Round(mediaMin.Value / 60.0)
            : 0;

        return Ok(new RelatorioChamadoDTO
        {
            AbertosNaUltimaHora = abertosUltimaHora,
            Pendentes = pendentes,
            EmAndamento = andamento,
            Concluido = concluidos,
            Cancelado = cancelados,
            TempoMedioConclusaoHoras = tempoMedioHoras
        });
    }
}
