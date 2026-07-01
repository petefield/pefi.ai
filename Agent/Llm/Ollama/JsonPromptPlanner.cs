using LocalAgent.Agent.Core;
using LocalAgent.Agent.Util;
using LocalAgent.Console.Agent.Util;
using Spectre.Console;
using System.Text;
using System.Text.Json;

namespace LocalAgent.Agent.Llm.Ollama;

public sealed class JsonPromptPlanner : IAgentPlanner
{
    private readonly OllamaClient _ollamaClient;
    private static readonly string _systemPromptTemplate = LoadSystemPromptTemplate();

    public JsonPromptPlanner(OllamaClient ollamaClient)
    {
        _ollamaClient = ollamaClient;
    }

    private static string LoadSystemPromptTemplate()
    {
        var promptPath = Path.Combine(AppContext.BaseDirectory, "system-prompt.md");
        
        if (!File.Exists(promptPath))
        {
            throw new FileNotFoundException($"System prompt file not found at: {promptPath}");
        }

        return File.ReadAllText(promptPath);
    }

    public async Task<AgentAction> GetNextActionAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyCollection<IAgentTool> tools,
        AgentOptions options,
        CancellationToken cancellationToken)
    {
        var systemMessage = BuildSystemMessage(tools);
        var fullMessages = new List<AgentMessage> { new(AgentRole.System, systemMessage) };
        fullMessages.AddRange(messages);

        var response = "";
        await foreach(var chunk in _ollamaClient.StreamOllama(
            options.Model,
            fullMessages,
            options.Temperature,
            cancellationToken))
        {

            if (!string.IsNullOrEmpty( chunk.Thinking))
            {
                Con.WriteItalic(chunk.Thinking, ConsoleColor.DarkYellow);
            }

            else if(!string.IsNullOrEmpty(chunk.Response))
            {
                Con.WriteItalic(chunk.Response, ConsoleColor.Blue);

                response += chunk.Response;
            }

            if (chunk.Done)
            {
                return ParseAction(response, tools);
            }
        }

        return new AgentAction(AgentActionType.Final, "Errr.  I stopped");
    }

    private static string BuildSystemMessage(IReadOnlyCollection<IAgentTool> tools)
    {
        var toolDescriptions = tools.Select(tool => new
        {
            name = tool.Name,
            description = tool.Description,
            parameters = tool.ParametersSchema
        });

        var toolsJson = JsonSerializer.Serialize(toolDescriptions, new JsonSerializerOptions { WriteIndented = true });

        return _systemPromptTemplate.Replace("{tools}", BuildToolIndex(tools));
    }

    private static string BuildToolIndex(IEnumerable<IAgentTool> tools)
{
    var groups = tools
        .OrderBy(t => GetToolGroup(t.Name))
        .ThenBy(t => t.Name)
        .GroupBy(t => GetToolGroup(t.Name));

    var sb = new StringBuilder();

    foreach (var group in groups)
    {
        sb.AppendLine($"{group.Key}:");

        foreach (var tool in group)
        {
            var summary = SummariseDescription(tool.Description);

            sb.AppendLine($"- {tool.Name}: {summary}");
        }

        sb.AppendLine();
    }

    return sb.ToString().Trim();
}
    private static string GetToolGroup(string toolName)
{
    if (toolName.StartsWith("mcp_google_calendar_", StringComparison.OrdinalIgnoreCase))
        return "Calendar";

    if (toolName.StartsWith("mcp_weather_", StringComparison.OrdinalIgnoreCase))
        return "Weather";

    if (toolName.StartsWith("mcp_filesystem_", StringComparison.OrdinalIgnoreCase))
        return "Filesystem";

    if (toolName.Contains("location", StringComparison.OrdinalIgnoreCase))
        return "Location";

    if (toolName.Contains("time", StringComparison.OrdinalIgnoreCase))
        return "Time";

    if (toolName.Contains("search", StringComparison.OrdinalIgnoreCase))
        return "Search";

    return "General";
}
    private static string SummariseDescription(string description)
{
    if (string.IsNullOrWhiteSpace(description))
        return "No description.";

    var firstLine = description
        .Replace("\r", "")
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault()
        ?.Trim();

    if (string.IsNullOrWhiteSpace(firstLine))
        return "No description.";

    return firstLine.Length <= 120
        ? firstLine
        : firstLine[..117] + "...";
}

    private static AgentAction ParseAction(string modelResponse, IReadOnlyCollection<IAgentTool> tools)
    {
        var json = JsonHelpers.ExtractFirstJsonObject(modelResponse);
        if (json is null)
        {
            return new AgentAction(AgentActionType.Final, Content: modelResponse.Trim());
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var type = root.TryGetProperty("type", out var typeProperty)
                ? typeProperty.GetString()
                : null;

            if (root.TryGetProperty("name", out var nameElement))
            {
                var toolName1 = nameElement.GetString();

                if (!string.IsNullOrWhiteSpace(toolName1) && tools.Any(x => string.Equals(x.Name, toolName1, StringComparison.OrdinalIgnoreCase )))
                {
                    var arguments = root.TryGetProperty("arguments", out var args)
                        ? args
                        : JsonDocument.Parse("{}").RootElement;

                   return new AgentAction(AgentActionType.ToolCall, ToolName: toolName1, Arguments: args);

                }
            }


            var toolName = root.TryGetProperty("tool", out var toolProperty)
                    ? toolProperty.GetString()
                    : null;

            if (string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase) || tools.Any(x => string.Equals(x.Name, toolName, StringComparison.OrdinalIgnoreCase )))
            {
                var args = root.TryGetProperty("arguments", out var argsProperty)
                    ? argsProperty.Clone()
                    : JsonSerializer.Deserialize<JsonElement>("{}");

                return new AgentAction(AgentActionType.ToolCall, ToolName: toolName, Arguments: args);
            }

            if (string.Equals(type, "final", StringComparison.OrdinalIgnoreCase))
            {
                var content = root.TryGetProperty("content", out var contentProperty)
                    ? contentProperty.GetString()
                    : string.Empty;

                return new AgentAction(AgentActionType.Final, Content: content ?? string.Empty);
            }
        }
        catch
        {
            // Fall through to treating the raw model response as a final answer.
        }

        return new AgentAction(AgentActionType.Final, Content: modelResponse.Trim());
    }
}
