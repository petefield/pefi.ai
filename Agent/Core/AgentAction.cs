using System.Text.Json;

namespace LocalAgent.Agent.Core;

public enum AgentActionType
{
    Final,
    ToolCall
}

public sealed record AgentAction(
    AgentActionType Type,
    string? Content = null,
    string? ToolName = null,
    JsonElement Arguments = default);
