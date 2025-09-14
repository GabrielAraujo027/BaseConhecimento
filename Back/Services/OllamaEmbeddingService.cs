using BaseConhecimento.Services.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;

namespace BaseConhecimento.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly IHttpClientFactory _httpFactory;

    private const string Model = "nomic-embed-text";

    public OllamaEmbeddingService(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public async Task<float[]> CreateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateClient("ollama");

        var payloadPrompt = new { model = Model, prompt = text };

        using var resp1 = await http.PostAsJsonAsync("api/embeddings", payloadPrompt, ct);
        var vec1 = await TryReadEmbedding(resp1, ct);
        if (vec1.Length > 0) return vec1;

        var payloadInput = new { model = Model, input = text };
        using var resp2 = await http.PostAsJsonAsync("api/embeddings", payloadInput, ct);
        var vec2 = await TryReadEmbedding(resp2, ct);
        if (vec2.Length > 0) return vec2;

        var payloadBatch = new { model = Model, input = new[] { text } };
        using var resp3 = await http.PostAsJsonAsync("api/embeddings", payloadBatch, ct);
        var vec3 = await TryReadEmbedding(resp3, ct, allowEmbeddingsPlural: true);
        return vec3;
    }

    private static async Task<float[]> TryReadEmbedding(HttpResponseMessage resp, CancellationToken ct, bool allowEmbeddingsPlural = false)
    {
        if (!resp.IsSuccessStatusCode)
            return Array.Empty<float>();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        if (root.TryGetProperty("embedding", out var one) && one.ValueKind == JsonValueKind.Array)
            return one.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();

        if (allowEmbeddingsPlural && root.TryGetProperty("embeddings", out var many) &&
            many.ValueKind == JsonValueKind.Array && many.GetArrayLength() > 0)
        {
            var first = many[0];
            if (first.ValueKind == JsonValueKind.Array)
                return first.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
        }

        return Array.Empty<float>();
    }
}
