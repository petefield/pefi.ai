using System.Text;
using System.Text.Json;
using LocalAgent.Agent.Core;

namespace LocalAgent.Agent.Tools;

public sealed class WebSearchTool : IAgentTool
{
    private readonly HttpClient _httpClient;

    public WebSearchTool(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Name => "web_search";

    public string Description =>
        "Searches the web for current information. Use this when the user asks about recent, current, or unknown information.";

    public JsonElement ParametersSchema => JsonSerializer.Deserialize<JsonElement>("""
    {
      "type": "object",
      "properties": {
        "query": {
          "type": "string",
          "description": "The web search query."
        }
      },
      "required": ["query"]
    }
    """);

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetProperty("query", out var queryElement))
        {
            return ToolResult.Fail("Missing required argument: query");
        }

        var query = queryElement.GetString();

        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResult.Fail("Query cannot be empty.");
        }

        var baseUrl =
            Environment.GetEnvironmentVariable("WEB_SEARCH_BASE_URL")
            ?? "http://192.168.1.142:8088";

        var url =
            $"{baseUrl.TrimEnd('/')}/search?q={Uri.EscapeDataString(query)}&format=json";

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ToolResult.Fail(
                    $"Search failed with HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("results", out var results))
            {
                return ToolResult.Ok("No results found.");
            }

            var output = new StringBuilder();
            var count = 0;

            foreach (var result in results.EnumerateArray())
            {
                if (count >= 5)
                {
                    break;
                }

                var title = GetString(result, "title") ?? "Untitled";
                var urlResult = GetString(result, "url") ?? "";
                var content = GetString(result, "content") ?? "";

                output.AppendLine($"{count + 1}. {title}");
                output.AppendLine($"   URL: {urlResult}");

                if (!string.IsNullOrWhiteSpace(content))
                {
                    output.AppendLine($"   Summary: {content}");
                }

                output.AppendLine();

                count++;
            }

            return count == 0
                ? ToolResult.Ok("No results found.")
                : ToolResult.Ok(output.ToString());
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Web search failed: {ex.Message}");
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.GetString()
            : null;
    }
}