using System.Text.Json;
using LocalAgent.Agent.Core;

namespace LocalAgent.Agent.Tools;

public sealed class ReadFileTool : IAgentTool
{
    private readonly FileSystemSecurity _security;
    private readonly long _maxBytes;

    public ReadFileTool(FileSystemSecurity security, long maxBytes = 128_000)
    {
        _security = security;
        _maxBytes = maxBytes;
    }

    public string Name => "read_file";
    public string Description => "Reads a UTF-8 text file under the allowed working directory.";
    public JsonElement ParametersSchema => ToolSchema.FromJson("""
    {
      "type": "object",
      "properties": {
        "path": {
          "type": "string",
          "description": "Relative path of the text file to read."
        }
      },
      "required": ["path"]
    }
    """);

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetProperty("path", out var pathProperty))
            return ToolResult.Fail("Missing required argument: path");

        var path = pathProperty.GetString();
        if (string.IsNullOrWhiteSpace(path))
            return ToolResult.Fail("Path cannot be empty.");

        var fullPath = _security.ResolveInsideRoot(path);
        if (!File.Exists(fullPath))
            return ToolResult.Fail($"File not found: {path}");

        var info = new FileInfo(fullPath);
        if (info.Length > _maxBytes)
            return ToolResult.Fail($"File is too large to read safely. Size={info.Length} bytes, max={_maxBytes} bytes.");

        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
        return ToolResult.Ok(content);
    }
}
