using BaseConhecimento.DTOs.Chat;
using BaseConhecimento.DTOs.Chat.Requests;

namespace BaseConhecimento.DTOs.Knowledge.Requests;

public class ChatKnowledgeRequestDTO
{
    public string Message { get; set; } = string.Empty;

    public List<ChatMessageDTO>? History { get; set; }
}

