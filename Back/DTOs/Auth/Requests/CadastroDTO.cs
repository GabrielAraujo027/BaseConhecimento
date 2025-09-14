namespace BaseConhecimento.DTOs.Auth.Requests
{
    public class CadastroDTO
    {
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string? FullName { get; set; }
    }
}
