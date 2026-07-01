namespace LocalAgent.Agent.Core;

public interface IAgentPlanner
{
    Task<AgentAction> GetNextActionAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyCollection<IAgentTool> tools,
        AgentOptions options,
        CancellationToken cancellationToken);
}
