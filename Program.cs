using LocalAgent.Agent.Core;
using LocalAgent.Agent.Llm.Ollama;
using LocalAgent.Agent.Tools;
using LocalAgent.Agent.Mcp;

var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://192.168.1.142:11434";
var model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen2.5:7b";

var options = new AgentOptions
{
    Model = model,
    MaxSteps = 8,
    Temperature = 0.1
};

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(ollamaUrl),
    Timeout = TimeSpan.FromMinutes(5)
};

var ollamaClient = new OllamaClient(httpClient);
var planner = new JsonPromptPlanner(ollamaClient);

var registry = new ToolRegistry();
var fsSecurity = new FileSystemSecurity(Directory.GetCurrentDirectory());
registry.Register(new GetCurrentTimeTool());
registry.Register(new ListDirectoryTool(fsSecurity));
registry.Register(new ReadFileTool(fsSecurity));
registry.Register(new WriteFileTool(fsSecurity));
registry.Register(new GetCurrentLocationTool());
registry.Register(new WebSearchTool(new HttpClient()));

await McpToolLoader.RegisterMcpToolsAsync(
    registry,
    "mcp-servers.json",
    CancellationToken.None);

var runtime = new AgentRuntime(planner, registry, options);

Console.WriteLine("LocalAgent.Console");
Console.WriteLine($"Ollama: {ollamaUrl}");
Console.WriteLine($"Model:  {options.Model}");
Console.WriteLine("Commands: /tools, /model <name>, /reset, /exit");
Console.WriteLine();

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
        continue;

    if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
        break;

    if (input.Equals("/tools", StringComparison.OrdinalIgnoreCase))
    {
        foreach (var tool in registry.Tools)
            Console.WriteLine($"- {tool.Name}: {tool.Description}");

        continue;
    }

    if (input.StartsWith("/model ", StringComparison.OrdinalIgnoreCase))
    {
        options.Model = input[7..].Trim();
        Console.WriteLine($"Model set to {options.Model}");
        continue;
    }

    if (input.Equals("/reset", StringComparison.OrdinalIgnoreCase))
    {
        runtime.Reset();
        Console.WriteLine("Conversation reset.");
        continue;
    }

    try
    {
        var answer = await runtime.RunAsync(input, CancellationToken.None);
        Console.WriteLine();
        Console.WriteLine(answer);
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}
