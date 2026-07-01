using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using LocalAgent.Agent.Core;

namespace LocalAgent.Agent.Llm.Ollama;

public sealed partial class OllamaClient
{
    private readonly HttpClient _httpClient;

    public OllamaClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async IAsyncEnumerable<OllamaChunk> StreamOllama(
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
                num_ctx = 8192,
                num_predict= 8192,
            }
        };

        using var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null && !cancellationToken.IsCancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            var chunk = new OllamaChunk(
                Response: root.TryGetProperty("response", out var r) ? r.GetString() : null,
                Thinking: root.TryGetProperty("thinking", out var t) ? t.GetString() : null,
                Done: root.TryGetProperty("done", out var d) && d.GetBoolean()
            );

            yield return chunk;

            if (chunk.Done)
                yield break;
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
