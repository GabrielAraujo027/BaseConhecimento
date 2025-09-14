using BaseConhecimento.Models.Chamados.Enums;
using System.ComponentModel.DataAnnotations;

namespace BaseConhecimento.Models.Chamados
{
    public class Chamado
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Titulo { get; set; }

        [Required, StringLength(500)]
        public string Descricao { get; set; }
        [Required]
        public StatusChamadoEnum StatusEnum { get; set; }
        [Required]
        public string SetorResponsavel { get; set; }
        [Required]
        public DateTime Data { get; set; }
        [Required]
        public string Solicitante { get; set; }
    }
}
