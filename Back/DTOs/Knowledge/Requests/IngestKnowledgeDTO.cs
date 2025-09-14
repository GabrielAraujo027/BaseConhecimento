using BaseConhecimento.DTOs.Common;
using BaseConhecimento.Models.Chamados.Enums;
using System.ComponentModel.DataAnnotations;

namespace BaseConhecimento.DTOs.Knowledge.Requests
{
    public class IngestKnowledgeDTO
    {
        public string Categoria { get; set; } = string.Empty;
        public string Subcategoria { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;
        public string PerguntasFrequentes { get; set; } = string.Empty;
    }
}
