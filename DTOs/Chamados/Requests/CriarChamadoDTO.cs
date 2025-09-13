using BaseConhecimento.DTOs.Common;
using BaseConhecimento.Models.Chamados.Enums;
using System.ComponentModel.DataAnnotations;

namespace BaseConhecimento.DTOs.Chamados.Requests
{
    public class CriarChamadoDTO
    {
        [Required, StringLength(100)]
        public string Titulo { get; set; }

        [Required, StringLength(500)]
        public string Descricao { get; set; }
        [Required]
        public string SetorResponsavel { get; set; }
    }
}
