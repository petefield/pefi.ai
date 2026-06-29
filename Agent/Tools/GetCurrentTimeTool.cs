using System.Text.Json;
using LocalAgent.Agent.Core;

namespace LocalAgent.Agent.Tools;

public sealed class GetCurrentTimeTool : IAgentTool
{
    public string Name => "get_current_time";
    public string Description => "Gets the current local date and time.";
    public JsonElement ParametersSchema => ToolSchema.EmptyObject();

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        return Task.FromResult(ToolResult.Ok(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz")));
    }
}
