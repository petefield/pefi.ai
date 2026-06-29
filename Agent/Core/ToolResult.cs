namespace LocalAgent.Agent.Core;

public sealed record ToolResult(bool Success, string Content, string? Error = null)
{
    public static ToolResult Ok(string content) => new(true, content);
    public static ToolResult Fail(string error) => new(false, string.Empty, error);
}
