namespace BaseConhecimento.DTOs.Chat.Requests;

public class ChatMessageDTO
{
    public string Role { get; set; } = "user"; // "system" | "user" | "assistant"
    public string Content { get; set; } = "";
}
