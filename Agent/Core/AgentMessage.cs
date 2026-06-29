namespace LocalAgent.Agent.Core;

public enum AgentRole
{
    System,
    User,
    Assistant,
    Tool
}

public sealed record AgentMessage(AgentRole Role, string Content);
