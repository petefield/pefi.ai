using System.Runtime.CompilerServices;
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

        var response = await _ollamaClient.GenerateAsync(
            options.Model,
            fullMessages,
            options.Temperature,
            cancellationToken);

        return ParseAction(response, tools);
    }

    public async IAsyncEnumerable<string> StreamFinalResponseAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyCollection<IAgentTool> tools,
        AgentOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var systemMessage = BuildSystemMessage(tools);
        var fullMessages = new List<AgentMessage> { new(AgentRole.System, systemMessage) };
        fullMessages.AddRange(messages);

        var fullResponse = new System.Text.StringBuilder();
        var isStreamingContent = false;
        var contentStarted = false;
        var bracesDepth = 0;
        var inString = false;
        var escapeNext = false;
        var fieldName = new System.Text.StringBuilder();
        var currentField = "";

        await foreach (var chunk in _ollamaClient.GenerateStreamAsync(
            options.Model,
            fullMessages,
            options.Temperature,
            cancellationToken))
        {
            fullResponse.Append(chunk);

            // Parse character by character to extract content field in real-time
            foreach (var c in chunk)
            {
                if (escapeNext)
                {
                    if (isStreamingContent)
                    {
                        // Handle escaped characters in content
                        if (c == 'n') yield return "\n";
                        else if (c == 't') yield return "\t";
                        else if (c == 'r') yield return "\r";
                        else if (c == '"') yield return "\"";
                        else if (c == '\\') yield return "\\";
                        else yield return c.ToString();
                    }
                    escapeNext = false;
                    continue;
                }

                if (c == '\\')
                {
                    escapeNext = true;
                    continue;
                }

                if (c == '"' && bracesDepth == 1)
                {
                    if (!inString)
                    {
                        // Starting a string (could be field name or value)
                        inString = true;
                        fieldName.Clear();
                    }
                    else
                    {
                        // Ending a string
                        inString = false;
                        
                        if (currentField == "")
                        {
                            // This was a field name
                            currentField = fieldName.ToString();
                            
                            // Check if we just found the content field
                            if (currentField == "content")
                            {
                                // Next string will be the content - start streaming it
                                isStreamingContent = true;
                                contentStarted = true;
                            }
                        }
                        else
                        {
                            // This was a field value
                            if (currentField == "content" && isStreamingContent)
                            {
                                // We've reached the end of content field
                                isStreamingContent = false;
                            }
                            currentField = "";
                        }
                        fieldName.Clear();
                    }
                    continue;
                }

                if (inString)
                {
                    if (currentField == "" || currentField == "type")
                    {
                        // Building field name or type value
                        fieldName.Append(c);
                    }
                    else if (isStreamingContent && currentField == "content")
                    {
                        // Stream content character by character
                        yield return c.ToString();
                    }
                    continue;
                }

                if (c == '{')
                {
                    bracesDepth++;
                }
                else if (c == '}')
                {
                    bracesDepth--;
                }
            }
        }

        // If we didn't stream any content, fallback to parsing complete response
        if (!contentStarted)
        {
            var completeResponse = fullResponse.ToString();
            var json = JsonHelpers.ExtractFirstJsonObject(completeResponse);
            
            if (json != null)
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("type", out var typeProperty) &&
                    typeProperty.GetString() == "final" &&
                    doc.RootElement.TryGetProperty("content", out var contentProperty))
                {
                    var content = contentProperty.GetString() ?? string.Empty;
                    yield return content;
                    yield break;
                }
            }

            // Last resort: return the raw response
            yield return completeResponse;
        }
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
