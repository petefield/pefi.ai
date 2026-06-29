using System.Text.Json;
using LocalAgent.Agent.Core;
using LocalAgent.Agent.Util;

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
        var promptPath = Path.Combine(AppContext.BaseDirectory, "system-prompt.txt");
        
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

        var response = await _ollamaClient.GenerateAsync(
            options.Model,
            fullMessages,
            options.Temperature,
            cancellationToken);

        return ParseAction(response, tools);
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

        return _systemPromptTemplate.Replace("{tools}", toolsJson);
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
