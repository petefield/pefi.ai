namespace LocalAgent.Agent.Mcp;

public sealed class McpServersConfig
{
    public List<McpServerConfig> Servers { get; set; } = [];
}

public sealed class McpServerConfig
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "stdio";

    // For stdio MCP servers
    public string Command { get; set; } = "";
    public List<string> Args { get; set; } = [];
    public Dictionary<string, string?> Env { get; set; } = [];

    // For remote MCP servers
    public string? Url { get; set; }

    // Optional startup timeout
    public int TimeoutSeconds { get; set; } = 30;
}