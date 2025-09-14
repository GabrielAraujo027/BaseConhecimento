namespace BaseConhecimento.Services.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> CreateEmbeddingAsync(string text, CancellationToken ct = default);
}
