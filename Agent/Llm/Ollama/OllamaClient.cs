using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using LocalAgent.Agent.Core;

namespace LocalAgent.Agent.Llm.Ollama;

public sealed class OllamaClient
{
    private readonly HttpClient _httpClient;

    public OllamaClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GenerateAsync(
        string model,
        IReadOnlyList<AgentMessage> messages,
        double temperature,
        CancellationToken cancellationToken)
    {
        var prompt = string.Join("\n\n", messages.Select(ToPromptBlock));

        var request = new
        {
            model,
            prompt,
            stream = false,
            options = new
            {
                temperature,         
                num_ctx = 8192,
                num_predict= 81920,
            }
        };

        using var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama request failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{raw}");


        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("response", out var responseProperty))
            throw new InvalidOperationException($"Ollama response did not contain a response property:\n{raw}");

        return responseProperty.GetString() ?? string.Empty;
    }

    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string model,
        IReadOnlyList<AgentMessage> messages,
        double temperature,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var prompt = string.Join("\n\n", messages.Select(ToPromptBlock));

        var request = new
        {
            model,
            prompt,
            stream = true,
            options = new
            {
                temperature,
                num_ctx = 8192
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = JsonContent.Create(request)
        };

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Ollama request failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{error}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("response", out var responseProperty))
            {
                var chunk = responseProperty.GetString();
                if (!string.IsNullOrEmpty(chunk))
                    yield return chunk;
            }

            if (doc.RootElement.TryGetProperty("done", out var doneProperty) && doneProperty.GetBoolean())
                break;
        }
    }

    private static string ToPromptBlock(AgentMessage message)
    {
        var role = message.Role switch
        {
            AgentRole.System => "SYSTEM",
            AgentRole.User => "USER",
            AgentRole.Assistant => "ASSISTANT",
            AgentRole.Tool => "TOOL OBSERVATION",
            _ => "MESSAGE"
        };

        return $"{role}:\n{message.Content}";
    }
}
