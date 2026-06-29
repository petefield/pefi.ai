using System.Text.Json;
using LocalAgent.Agent.Core;

namespace LocalAgent.Agent.Tools;

public sealed class ListDirectoryTool : IAgentTool
{
    private readonly FileSystemSecurity _security;

    public ListDirectoryTool(FileSystemSecurity security)
    {
        _security = security;
    }

    public string Name => "list_directory";
    public string Description => "Lists files and directories under the allowed working directory.";
    public JsonElement ParametersSchema => ToolSchema.FromJson("""
    {
      "type": "object",
      "properties": {
        "path": {
          "type": "string",
          "description": "Relative path to list. Use '.' for the current directory."
        }
      },
      "required": ["path"]
    }
    """);

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var path = arguments.TryGetProperty("path", out var pathProperty)
            ? pathProperty.GetString() ?? "."
            : ".";

        var fullPath = _security.ResolveInsideRoot(path);
        if (!Directory.Exists(fullPath))
            return Task.FromResult(ToolResult.Fail($"Directory not found: {path}"));

        var entries = Directory.EnumerateFileSystemEntries(fullPath)
            .OrderBy(x => x)
            .Select(entry =>
            {
                var name = Path.GetFileName(entry);
                return Directory.Exists(entry) ? $"[dir]  {name}" : $"[file] {name}";
            });

        return Task.FromResult(ToolResult.Ok(string.Join(Environment.NewLine, entries)));
    }
}
