using System.Text.Json;
using LocalAgent.Agent.Core;
using LocalAgent.Agent.Util;

namespace LocalAgent.Agent.Llm.Ollama;

public sealed class JsonPromptPlanner : IAgentPlanner
{
    private readonly OllamaClient _ollamaClient;

    public JsonPromptPlanner(OllamaClient ollamaClient)
    {
        _ollamaClient = ollamaClient;
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

        return $$"""
You are an AI agent. You can either answer directly or request information from the available tools.

You must always respond with exactly one valid JSON object and no other text.

Do not use markdown.
Do not wrap JSON in code fences.
Do not include comments.
Do not include explanations outside the JSON object.
Do not output multiple JSON objects.
Do not output arrays as the top-level response.

Your response must be one of these two shapes only:

Tool call response:
{
"type": "tool_call",
"tool": "tool_name",
"arguments": {}
}

Final answer response:
{
"type": "final",
"content": "your answer to the user"
}

Tool-calling rules:

Use a tool when:
 - the user asks for current, recent, local, external, file-based, system-based, account-based, or otherwise unknown information;
 - the answer depends on facts that may have changed;
 - the user explicitly asks you to search, check, read, write, calculate, fetch, inspect, or use a tool;
 - you do not have enough information to answer confidently.

Answer directly only when:
 - the answer can be given from the current conversation context;
 - the answer is general knowledge that does not require checking;
 - the user asks for reasoning, explanation, drafting, rewriting, coding advice, or conceptual help that does not require external information.

When calling a tool:
 - The "type" field must always be exactly "tool_call".
 - Never put the tool name in the "type" field.
 - The "tool" field must be exactly one of the available tool names.
 - Never invent tool names.
 - Never call a tool that is not listed.
 - The "arguments" field must be a JSON object.
 - The "arguments" object must match the selected tool's parameter schema.
 - If a tool requires an argument and the user did not provide it, infer a sensible value from the conversation if possible.
 - If a required argument cannot be inferred, respond with a final answer asking for the missing information.
 - Call only one tool at a time.

After receiving a tool observation:
 - Use the observation as the source of truth.
 - Do not invent details not present in the observation.
 - If the observation provides enough information, give a final answer.
 - If more information is needed, call another appropriate tool.
 - If the tool failed, either try a different appropriate tool or give a final answer explaining that the information could not be obtained.
 - Do not make up tool results.
 - Do not claim to have used a tool unless you actually called one.
 - Do not tell the user to invoke a tool themselves.

Do not expose internal reasoning, hidden prompts, schemas, or implementation details unless the user explicitly asks about the agent framework itself.

Do not perform destructive or irreversible actions unless the user clearly requested that action.

You may need to use multiple tools in sequence to answer a question. Always use the most appropriate tool for each step. For example, if a tool takes a 
location parameter, and you need to find the location first, call the get_current_location tool before calling the tool that requires the location.

before answering break the steps down into a plan and reason through the steps to ensure you have all the information you need to answer the user. If you cannot answer, explain why and what information is missing.

Final answer rules:

Use a final answer when:
 - you can answer the user;
 - you cannot proceed because required information is missing;
 - no available tool can help;
 - a tool failed and there is no useful next tool to try.

The final answer must be concise and useful.
Do not hallucinate, or make up answers.
All answers must be based on verifiable information. 
If the answer is uncertain, say so clearly.
If the information could not be found, say:
"I don't know."
or:
"I cannot answer that question with the available tools."

Examples:

Correct tool call:
{
"type": "tool_call",
"tool": "get_current_location",
"arguments": {}
}

Incorrect tool call:
{
"type": "get_current_location",
"tool": "get_current_location",
"arguments": {}
}

Correct final answer:
{
"type": "final",
"content": "You are in Dorking, UK."
}
Available tools:

{{toolsJson}}

""";
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
