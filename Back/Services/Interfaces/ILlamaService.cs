namespace BaseConhecimento.Services.Interfaces
{
    public interface ILlamaService
    {
        Task<string> GenerateAsync(string prompt, CancellationToken ct = default);
    }
}
