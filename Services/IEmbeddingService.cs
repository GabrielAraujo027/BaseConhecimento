namespace BaseConhecimento.Services;

public interface IEmbeddingService
{
    Task<float[]> CreateEmbeddingAsync(string text, CancellationToken ct = default);
}
