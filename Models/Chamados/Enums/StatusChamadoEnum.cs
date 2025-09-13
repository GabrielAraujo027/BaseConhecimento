using System.ComponentModel;

namespace BaseConhecimento.Models.Chamados.Enums
{
    public enum StatusChamadoEnum
    {
        Pendente = 0,
        [Description("Em andamento")]
        EmAndamento = 1,
        Concluido = 2,
        Cancelado = 3
    }
}
