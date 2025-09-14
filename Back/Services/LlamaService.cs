using System.Text.Json;
using BaseConhecimento.Services.Interfaces;

namespace BaseConhecimento.Services
{
    public sealed class LlamaService : ILlamaService
    {
        private readonly HttpClient _http;

        private sealed record GenerateReq(string model, string prompt, bool stream);
        private sealed record GenerateResp(string? response);

        public LlamaService(IHttpClientFactory factory)
        {
            _http = factory.CreateClient("ollama");
        }

        public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
        {
            var req = new GenerateReq(model: "llama3", prompt: prompt, stream: false);

            using var res = await _http.PostAsJsonAsync("api/generate", req, cancellationToken: ct);
            res.EnsureSuccessStatusCode();

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (doc.RootElement.TryGetProperty("response", out var r) && r.ValueKind == JsonValueKind.String)
                return r.GetString() ?? string.Empty;

            try
            {
                var parsed = await res.Content.ReadFromJsonAsync<GenerateResp>(cancellationToken: ct);
                return parsed?.response ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
