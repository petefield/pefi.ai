# LocalAgent.Console

A lightweight local AI agent runtime powered by Ollama. It uses a simple JSON-based tool-calling protocol — the model either returns a `tool_call` or a `final` answer, and the runtime orchestrates execution in a loop.

## Features

- **ReAct-style agent loop** — plan → act → observe → repeat
- **Built-in tools** — file system, time, web search, geolocation
- **MCP support** — connect to external Model Context Protocol servers (stdio, SSE, HTTP)
- **Interactive REPL** — chat with the agent in a terminal session

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Ollama](https://ollama.com) running and accessible
- At least one model pulled:

```bash
ollama pull qwen2.5:7b
# or
ollama pull llama3.1:8b
```

## Quick Start

```bash
dotnet run --project LocalAgent.Console.csproj
```

## Debugging in VS Code

This repository includes VS Code debug configuration files under `.vscode/`.

1. Open the project folder in VS Code.
2. Install recommended extensions when prompted.
3. Set breakpoints in C# files.
4. Press `F5` and choose **.NET: Launch LocalAgent.Console**.

The debug profile runs the `build` task first, then launches `bin/Debug/net10.0/LocalAgent.Console.dll` in an external terminal.

Default configuration:

| Setting | Default |
|---------|---------|
| Ollama URL | `http://192.168.1.142:11434` |
| Model | `qwen2.5:7b` |
| Max Steps | `8` |
| Temperature | `0.1` |

Override with environment variables:

```bash
OLLAMA_URL=http://localhost:11434 OLLAMA_MODEL=llama3.1:8b dotnet run
```

## Commands

| Command | Description |
|---------|-------------|
| `/tools` | List all registered tools |
| `/model <name>` | Change model for this session |
| `/reset` | Clear conversation state |
| `/exit` | Quit |

## Built-in Tools

| Tool | Description |
|------|-------------|
| `get_current_time` | Returns the current date and time |
| `get_current_location` | Returns geolocation (country, region, city, coordinates) |
| `list_directory` | Lists files in a directory (sandboxed to working directory) |
| `read_file` | Reads file contents (sandboxed) |
| `write_file` | Writes content to a file (sandboxed) |
| `web_search` | Performs a web search and returns results |

Filesystem tools are restricted to the current working directory and its children via `FileSystemSecurity`.

## MCP (Model Context Protocol)

External tool servers can be registered via `mcp-servers.json`:

```json
{
  "servers": [
	{
	  "name": "weather",
	  "type": "http",
	  "url": "http://localhost:3002/mcp",
	  "timeoutSeconds": 30
	}
  ]
}
```

Supported transport types: `stdio`, `sse`, `http` / `streamablehttp`.

MCP tools are automatically discovered and registered at startup with the prefix `mcp_<server>_<tool>`.

## Project Structure

```
LocalAgent.Console/
├── Program.cs                      # Entry point and REPL
├── Agent/
│   ├── Core/                       # Runtime, planner, tool interfaces
│   ├── Llm/Ollama/                 # Ollama HTTP client and JSON planner
│   ├── Tools/                      # Built-in tool implementations
│   └── Util/                       # Console helpers, JSON utilities
├── mcp/                            # MCP client, config, tool adapter
├── mcp-servers.json                # MCP server configuration
└── LocalAgent.Console.csproj
```

## Example Prompts

```text
What time is it?
```

```text
Where am I located?
```

```text
List the files in this directory and create a NOTES.md summary.
```

```text
Search the web for the latest .NET 8 features.
```

## Architecture

See [agent.md](agent.md) for detailed architecture documentation, extension guidance, and design decisions.
