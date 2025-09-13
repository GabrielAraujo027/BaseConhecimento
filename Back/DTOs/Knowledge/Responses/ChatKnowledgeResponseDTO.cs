namespace BaseConhecimento.DTOs.Knowledge;

public class ChatKnowledgeResponseDTO
{
    /// <summary>Mensagem a exibir para o usuário (resposta ou pergunta de esclarecimento).</summary>
    public string Reply { get; set; } = string.Empty;
    /// <summary>Se abriu chamado por falta de resposta, retorna o Id do chamado.</summary>
    public int? TicketId { get; set; } = null;
}
