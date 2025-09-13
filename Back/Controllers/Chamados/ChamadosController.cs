using BaseConhecimento.Data;
using BaseConhecimento.DTOs.Chamados.Requests;
using BaseConhecimento.Models.Chamados;
using BaseConhecimento.Models.Chamados.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseConhecimento.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChamadosController : ControllerBase
{
    private readonly AppDbContext _ctx;
    public ChamadosController(AppDbContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Chamado>>> GetAll()
        => await _ctx.Chamados.AsNoTracking().ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Chamado>> GetById(int id)
    {
        var item = await _ctx.Chamados.FindAsync(id);
        return item is null ? NotFound() : item;
    }

    [HttpPost]
    public async Task<ActionResult<Chamado>> Create(CriarChamadoDTO request)
    {
        var chamado = new Chamado
        {
            Titulo = request.Titulo,
            Descricao = request.Descricao,
            StatusEnum = StatusChamadoEnum.Pendente,
            SetorResponsavel = request.SetorResponsavel
        };

        _ctx.Chamados.Add(chamado);
        await _ctx.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = chamado.Id }, chamado);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, AlterarChamadoDTO request)
    {
        if (id != request.Id) return BadRequest();

        var entity = await _ctx.Chamados.FindAsync(id);
        if (entity is null) return NotFound();

        entity.StatusEnum = request.StatusEnum;
        entity.SetorResponsavel = request.SetorResponsavel;

        await _ctx.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _ctx.Chamados.FindAsync(id);
        if (entity is null) return NotFound();

        _ctx.Chamados.Remove(entity);
        await _ctx.SaveChangesAsync();
        return NoContent();
    }
}
