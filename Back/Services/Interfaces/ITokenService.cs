using BaseConhecimento.Models.Auth;

namespace BaseConhecimento.Services.Interfaces
{
    public interface ITokenService
    {
        Task<string> GenerateAsync(ApplicationUser user, IEnumerable<string> roles, DateTime expiresAt);
    }
}
