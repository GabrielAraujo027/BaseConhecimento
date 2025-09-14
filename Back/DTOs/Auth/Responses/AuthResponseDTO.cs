namespace BaseConhecimento.DTOs.Auth.Responses
{
    public class AuthResponseDTO
    {
        public string Token { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
        public string Email { get; set; } = default!;
        public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();
    }
}
