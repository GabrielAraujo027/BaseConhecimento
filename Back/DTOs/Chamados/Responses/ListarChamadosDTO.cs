using BaseConhecimento.Models.Chamados.Enums;

namespace BaseConhecimento.DTOs.Chamados.Responses
{
    public class ListarChamadosResponse
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public StatusChamadoEnum Status { get; set; }
        public string SetorResponsavel { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; }
        public DateTime? DataConclusao { get; set; }
        public string? Solicitante { get; set; }
    }
}
