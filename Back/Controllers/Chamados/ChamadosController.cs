using BaseConhecimento.Data;
using BaseConhecimento.DTOs.Chamados.Requests;
using BaseConhecimento.Models.Chamados;
using BaseConhecimento.Models.Chamados.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BaseConhecimento.Controllers;

[ApiController]
[Authorize(Roles = "Atendente")]
[Route("api/[controller]")]
public class ChamadosController : ControllerBase
{
    private readonly AppDbContext _ctx;
    public ChamadosController(AppDbContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Chamado>>> GetAll([FromQuery] FiltrarChamadoRequest request)
    {
        if(!string.IsNullOrEmpty(request.SetorResponsavel))
        {
            _ctx.Chamados.Where(x => x.SetorResponsavel == request.SetorResponsavel);
        }
        
        if(request.StatusEnum is not null)
        {
            _ctx.Chamados.Where(x => x.StatusEnum == request.StatusEnum);
        }

        return await _ctx.Chamados.AsNoTracking().ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Chamado>> GetById(int id)
    {
        var item = await _ctx.Chamados.FindAsync(id);
        return item is null ? NotFound() : item;
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
