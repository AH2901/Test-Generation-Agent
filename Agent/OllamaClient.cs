using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiTestGenerationAgent.Agent;

public sealed class OllamaClient
{
    private readonly HttpClient _http;

    public OllamaClient(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(90)
        };
    }

    public async Task CheckHealthAsync(string model)
    {
        using var response = await _http.GetAsync("api/tags");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        if (payload is null)
        {
            throw new InvalidOperationException("Ollama returned an empty response.");
        }

        var root = payload.RootElement;

        if (!root.TryGetProperty("models", out var models) ||
            models.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Ollama returned an unexpected /api/tags response.");
        }

        var found = models.EnumerateArray()
            .Select(x => x.TryGetProperty("name", out var name)
                ? name.GetString()
                : null)
            .Any(name => string.Equals(
                name,
                model,
                StringComparison.OrdinalIgnoreCase));

        if (!found)
        {
            throw new InvalidOperationException(
                $"Ollama is running, but model '{model}' is not installed. " +
                $"Run: ollama pull {model}");
        }
    }

    public async Task<OllamaMessage> ChatAsync(
        string model,
        List<OllamaMessage> messages,
        object[] tools,
        int numCtx = 8192)
    {
        var request = new
        {
            model,
            messages,
            stream = false,
            think = false,
            tools,
            options = new
            {
                num_ctx = numCtx,
                temperature = 0.1
            }
        };

        using var response = await _http.PostAsJsonAsync("api/chat", request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Ollama /api/chat failed ({(int)response.StatusCode}): {body}");
        }

        var parsed = JsonSerializer.Deserialize<OllamaResponse>(body, JsonDefaults.Options)
                     ?? throw new InvalidOperationException("Could not parse Ollama response.");

        return parsed.Message ?? throw new InvalidOperationException("Ollama response had no message.");
    }
}

public sealed class OllamaResponse
{
    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; set; }
}

public sealed class OllamaMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("tool_calls")]
    public List<OllamaToolCall>? ToolCalls { get; set; }

    [JsonPropertyName("tool_name")]
    public string? ToolName { get; set; }
}

public sealed class OllamaToolCall
{
    [JsonPropertyName("function")]
    public OllamaFunctionCall Function { get; set; } = new();
}

public sealed class OllamaFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; set; }
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
