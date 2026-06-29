using System.Text.Json;

namespace LocalAgent.Agent.Tools;

public static class ToolSchema
{
    public static JsonElement EmptyObject()
        => JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {},
          "required": []
        }
        """);

    public static JsonElement FromJson(string json)
        => JsonSerializer.Deserialize<JsonElement>(json);
}
