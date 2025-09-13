using BaseConhecimento.DTOs.Chat;
using BaseConhecimento.DTOs.Chat.Requests;

namespace BaseConhecimento.DTOs.Knowledge.Requests;

public class ChatKnowledgeRequestDTO
{
    public string Message { get; set; } = string.Empty;

    // Opcional: histórico para o front manter contexto (não é usado no matching agora)
    public List<ChatMessageDTO>? History { get; set; }
}

