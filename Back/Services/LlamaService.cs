using BaseConhecimento.Services.Interfaces;

namespace BaseConhecimento.Services
{
    public class LlamaService : ILlamaService
    {
        private readonly HttpClient _http;

        public LlamaService(HttpClient http)
        {
            _http = http;
        }

        public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
        {
            var body = new
            {
                model = "llama3",
                prompt = prompt,
                stream = false
            };

            var resp = await _http.PostAsJsonAsync("http://localhost:11434/api/generate", body, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken: ct);
            return json?["response"]?.ToString() ?? "(sem resposta gerada)";
        }
    }
}
