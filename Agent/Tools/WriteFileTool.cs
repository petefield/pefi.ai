using System.Text.Json;
using LocalAgent.Agent.Core;

namespace LocalAgent.Agent.Tools;

public sealed class WriteFileTool : IAgentTool
{
    private readonly FileSystemSecurity _security;

    public WriteFileTool(FileSystemSecurity security)
    {
        _security = security;
    }

    public string Name => "write_file";
    public string Description => "Writes a UTF-8 text file under the allowed working directory. Creates parent directories if needed.";
    public JsonElement ParametersSchema => ToolSchema.FromJson("""
    {
      "type": "object",
      "properties": {
        "path": {
          "type": "string",
          "description": "Relative file path to write."
        },
        "content": {
          "type": "string",
          "description": "Full text content to write."
        }
      },
      "required": ["path", "content"]
    }
    """);

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetProperty("path", out var pathProperty))
            return ToolResult.Fail("Missing required argument: path");

        if (!arguments.TryGetProperty("content", out var contentProperty))
            return ToolResult.Fail("Missing required argument: content");

        var path = pathProperty.GetString();
        var content = contentProperty.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(path))
            return ToolResult.Fail("Path cannot be empty.");

        var fullPath = _security.ResolveInsideRoot(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content, cancellationToken);

        return ToolResult.Ok($"Wrote {content.Length} characters to {path}.");
    }
}
