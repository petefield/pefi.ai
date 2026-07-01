using System.Text.Json;
using LocalAgent.Agent.Core;
using ModelContextProtocol.Client;

namespace LocalAgent.Agent.Mcp;


public static class McpToolLoader
{
    public static async Task RegisterMcpToolsAsync(
        ToolRegistry registry,
        string configPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(configPath))
        {
            global::System.Console.WriteLine($"No MCP config found at {configPath}");
            return;
        }

        var json = await File.ReadAllTextAsync(configPath, cancellationToken);

        var config = JsonSerializer.Deserialize<McpServersConfig>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (config is null || config.Servers.Count == 0)
        {
            global::System.Console.WriteLine("No MCP servers configured.");
            return;
        }

        foreach (var server in config.Servers)
        {
            try
            {
                await RegisterServerToolsAsync(
                    registry,
                    server,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                global::System.Console.WriteLine($"MCP server '{server.Name}' timed out during startup.");
            }
            catch (Exception ex)
            {
                global::System.Console.WriteLine($"Failed to register MCP server '{server.Name}': {ex.Message}");
            }
        }
    }

    private static async Task RegisterServerToolsAsync(
        ToolRegistry registry,
        McpServerConfig server,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(server.Name))
        {
            throw new InvalidOperationException("MCP server is missing name.");
        }

        global::System.Console.WriteLine($"Starting MCP server: {server.Name}");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(server.TimeoutSeconds <= 0 ? 30 : server.TimeoutSeconds));

        var clientTransport = CreateTransport(server);

        global::System.Console.WriteLine($"Creating MCP client for: {server.Name}");

        var client = await McpClient.CreateAsync(
    clientTransport,
    cancellationToken: timeoutCts.Token);

        global::System.Console.WriteLine($"MCP client created for: {server.Name}");
        global::System.Console.WriteLine($"Listing MCP tools for: {server.Name}");

        var tools = await client.ListToolsAsync(
            cancellationToken: timeoutCts.Token);

        global::System.Console.WriteLine($"MCP tools found for {server.Name}: {tools.Count}");

        foreach (var tool in tools)
        {
            var adapter = new McpToolAdapter(client, tool, server.Name);
            registry.Register(adapter);

            global::System.Console.WriteLine($"Registered MCP tool: {adapter.Name}");
        }
    }

    private static IClientTransport CreateTransport(McpServerConfig server)
    {
        var type = string.IsNullOrWhiteSpace(server.Type)
            ? "stdio"
            : server.Type.Trim().ToLowerInvariant();

        return type switch
        {
            "stdio" => CreateStdioTransport(server),
            "sse" => CreateSseTransport(server),
            "http" => CreateSseTransport(server),
            "streamablehttp" => CreateSseTransport(server),

            _ => throw new InvalidOperationException(
                $"Unsupported MCP transport type '{server.Type}' for server '{server.Name}'.")
        };
    }

    private static IClientTransport CreateStdioTransport(McpServerConfig server)
    {
        if (string.IsNullOrWhiteSpace(server.Command))
        {
            throw new InvalidOperationException(
                $"MCP stdio server '{server.Name}' is missing command.");
        }

        return new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = server.Name,
            Command = server.Command,
            Arguments = server.Args,
            EnvironmentVariables = server.Env.ToDictionary(
                static kvp => kvp.Key,
                static kvp => (string?)kvp.Value)
        });
    }

    private static IClientTransport CreateSseTransport(McpServerConfig server)
    {
        if (string.IsNullOrWhiteSpace(server.Url))
        {
            throw new InvalidOperationException(
                $"MCP HTTP/SSE server '{server.Name}' is missing url.");
        }

        return new HttpClientTransport(new HttpClientTransportOptions
        {
            Name = server.Name,
            Endpoint = new Uri(server.Url)
        });
    }
}