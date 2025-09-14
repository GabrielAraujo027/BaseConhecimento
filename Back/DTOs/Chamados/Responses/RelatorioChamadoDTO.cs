namespace BaseConhecimento.DTOs.Chamados.Responses
{
    public class RelatorioChamadoDTO
    {
        public int AbertosNaUltimaHora { get; set; }
        public int Pendentes { get; set; }
        public int EmAndamento { get; set; }
        public int Concluido { get; set; }
        public int Cancelado { get; set; }
        public int TempoMedioConclusaoHoras { get; set; }
    }
}
