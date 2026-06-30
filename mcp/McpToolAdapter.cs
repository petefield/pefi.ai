using LocalAgent.Agent.Core;
using LocalAgent.Agent.Tools;
using ModelContextProtocol.Client;
using System.Text.Json;

namespace LocalAgent.Agent.Mcp;

public sealed class McpToolAdapter : IAgentTool
{
    private readonly McpClient _client;
    private readonly McpClientTool _tool;
    private readonly string _serverName;

    public McpToolAdapter(
        McpClient client,
        McpClientTool tool,
        string serverName)
    {
        _client = client;
        _tool = tool;
        _serverName = serverName;
    }

    public string Name => $"mcp_{_serverName}_{_tool.Name}";

    public string Description =>
        string.IsNullOrWhiteSpace(_tool.Description)
            ? $"MCP tool '{_tool.Name}' from server '{_serverName}'."
            : _tool.Description;

    public JsonElement ParametersSchema
    {
        get
        {
            //if (_tool.JsonSchema is null)
            //{
            //    return ToolSchema.EmptyObject();
            //}

            return JsonSerializer.SerializeToElement(_tool.JsonSchema);
        }
    }

    public async Task<ToolResult> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var argumentsDict = arguments.ValueKind == JsonValueKind.Undefined || arguments.ValueKind == JsonValueKind.Null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments);

        var result = await _client.CallToolAsync(
            _tool.Name,
            argumentsDict,
            cancellationToken: cancellationToken);

              var content = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true
        });


        if (result.IsError ?? false)
        { 
                return ToolResult.Fail(content);
        }
  
    
        return ToolResult.Ok(content);
    }
}