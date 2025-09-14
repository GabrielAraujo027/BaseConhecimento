using BaseConhecimento.DTOs.Chat.Requests;

namespace BaseConhecimento.DTOs.Chat;

public class ChatRequestDTO
{
    public string Message { get; set; } = "";
    public List<ChatMessageDTO>? History { get; set; }
}
