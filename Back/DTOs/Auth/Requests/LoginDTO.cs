namespace BaseConhecimento.DTOs.Auth.Requests
{
    public class LoginDTO
    {
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
    }
}
