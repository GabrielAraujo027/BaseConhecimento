using System.Net.Http.Json;
using BaseConhecimento.DTOs.Chat;
using Microsoft.AspNetCore.Mvc;

namespace BaseConhecimento.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private const string DefaultModel = "gpt-4.1-mini"; // pode trocar para outro modelo

    public ChatController(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponseDTO>> Chat([FromBody] ChatRequestDTO req, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { error = "Message é obrigatório." });

        var http = _httpFactory.CreateClient("openai");

        // Monta input da Responses API
        var inputBlocks = new List<object>();
        inputBlocks.Add(new { role = "system", content = "Você é um assistente objetivo e útil." });

        if (req.History is not null)
        {
            foreach (var m in req.History)
                inputBlocks.Add(new { role = m.Role, content = m.Content });
        }

        inputBlocks.Add(new { role = "user", content = req.Message });

        var payload = new
        {
            model = DefaultModel,
            input = inputBlocks
        };

        using var resp = await http.PostAsJsonAsync("v1/responses", payload, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            return StatusCode((int)resp.StatusCode, new { error = err });
        }

        var data = await resp.Content.ReadFromJsonAsync<dynamic>(cancellationToken: ct);

        // Extrair texto do output
        string text = "";
        var output = data?.output;
        if (output != null)
        {
            foreach (var o in output)
            {
                if ((string)o.type == "message")
                {
                    foreach (var c in o.content)
                    {
                        if ((string)c.type == "output_text")
                            text += (string)c.text;
                    }
                }
            }
        }

        return Ok(new ChatResponseDTO
        {
            Reply = string.IsNullOrWhiteSpace(text) ? "(sem texto retornado)" : text
        });
    }

    // OPCIONAL: streaming SSE (para "digitando...")
    [HttpGet("stream")]
    public async Task Stream([FromQuery] string message, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.ContentType = "text/event-stream";

        if (string.IsNullOrWhiteSpace(message))
        {
            await Response.WriteAsync("event: error\ndata: message ausente\n\n", ct);
            await Response.Body.FlushAsync(ct);
            return;
        }

        var http = _httpFactory.CreateClient("openai");

        var payload = new
        {
            model = DefaultModel,
            stream = true,
            input = new object[] {
                new { role = "system", content = "Você é um assistente objetivo e útil." },
                new { role = "user", content = message }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/responses")
        {
            Content = JsonContent.Create(payload)
        };

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            await Response.WriteAsync($"data: {line}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    [HttpPost("ollama")]
    public async Task<ActionResult<ChatResponseDTO>> ChatOllama([FromBody] ChatRequestDTO req, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { error = "Message é obrigatório." });

        var http = _httpFactory.CreateClient("ollama");

        var payload = new
        {
            model = "llama3",    // você pode trocar para "mistral", "gemma" etc.
            prompt = req.Message
        };

        using var resp = await http.PostAsJsonAsync("api/generate", payload, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            return StatusCode((int)resp.StatusCode, new { error = err });
        }

        var json = await resp.Content.ReadFromJsonAsync<dynamic>(cancellationToken: ct);
        string text = json?.response ?? "(sem resposta)";

        return Ok(new ChatResponseDTO { Reply = text });
    }
}
