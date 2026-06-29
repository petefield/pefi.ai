using System.Net.Http.Json;
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
                temperature
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
