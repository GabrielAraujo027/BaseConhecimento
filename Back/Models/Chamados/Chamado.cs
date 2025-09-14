using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BaseConhecimento.Models.Chamados.Enums;

namespace BaseConhecimento.Models.Chamados
{
    public class Chamado
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Titulo { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Descricao { get; set; } = string.Empty;

        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
        public DateTime? DataConclusao { get; set; } = DateTime.UtcNow;

        [StringLength(256)]
        public string? Solicitante { get; set; }

        [Required]
        public StatusChamadoEnum StatusEnum { get; set; } = StatusChamadoEnum.Pendente;

        [Required, StringLength(80)]
        public string SetorResponsavel { get; set; } = "TI";
    }
}
