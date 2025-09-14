using BaseConhecimento.Models.Chamados.Enums;

namespace BaseConhecimento.DTOs.Chamados.Requests
{
    public class FiltrarChamadoRequest
    {
        public StatusChamadoEnum? StatusEnum { get; set; }
        public string SetorResponsavel { get; set; }
    }
}
