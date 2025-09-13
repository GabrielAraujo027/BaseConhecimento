using BaseConhecimento.DTOs.Common;
using BaseConhecimento.Models.Chamados.Enums;
using System.ComponentModel.DataAnnotations;

namespace BaseConhecimento.DTOs.Chamados.Requests
{
    public class AlterarChamadoDTO
    {
        [Required]
        public int Id { get; set; }
        public StatusChamadoEnum StatusEnum { get; set; }
        public string SetorResponsavel { get; set; }
    }
}
