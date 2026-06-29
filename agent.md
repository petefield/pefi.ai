# Agent Architecture

This document describes the internal architecture of LocalAgent.Console, how the agent loop works, and how to extend it.

## Overview

LocalAgent.Console is a **ReAct-style** (Reason + Act) agent. It uses a large language model (via Ollama) to decide what actions to take, executes those actions as tool calls, and feeds the results back until the model produces a final answer.

```
User Input
	│
	▼
┌──────────────┐
│   Planner    │◄── LLM (Ollama)
│  (decides)   │
└──────┬───────┘
	   │
	   ▼
  ┌─────────┐       ┌─────────────┐
  │ tool_call│──────►│  Tool Exec  │
  └─────────┘       └──────┬──────┘
						   │
						   ▼
					┌─────────────┐
					│  Observation │──► fed back to planner
					└─────────────┘
	   │
	   ▼
  ┌─────────┐
  │  final   │──────► Response to User
  └─────────┘
```

## Core Components

### AgentRuntime (`Agent/Core/AgentRuntime.cs`)

The main orchestrator. It:

1. Accepts user input
2. Calls the planner in a loop (up to `MaxSteps`)
3. Dispatches tool calls to the registry
4. Collects observations (tool results) and appends them to the conversation
5. Returns the final answer

### IAgentPlanner (`Agent/Core/IAgentPlanner.cs`)

Abstraction for the LLM integration. Given the conversation history and available tools, it returns an `AgentAction` — either a `tool_call` or a `final` answer.

Current implementation: `JsonPromptPlanner` — sends a structured prompt to Ollama and parses a JSON response.

### ToolRegistry (`Agent/Core/ToolRegistry.cs`)

A simple dictionary of `IAgentTool` instances, keyed by name. Tools are registered at startup in `Program.cs`.

### IAgentTool (`Agent/Core/IAgentTool.cs`)

The tool interface:

```csharp
public interface IAgentTool
{
	string Name { get; }
	string Description { get; }
	JsonElement ParametersSchema { get; }
	Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);
}
```

Each tool declares a JSON Schema for its parameters, enabling the planner to understand what arguments are expected.

## Message Protocol

The agent uses a simple JSON protocol with the model. The planner expects one of two response shapes:

### Tool Call

```json
{
  "type": "tool_call",
  "tool": "read_file",
  "arguments": { "path": "README.md" }
}
```

### Final Answer

```json
{
  "type": "final",
  "content": "Here is what the file says..."
}
```

## Adding a New Tool

1. Create a class implementing `IAgentTool` in `Agent/Tools/`:

```csharp
public sealed class MyTool : IAgentTool
{
	public string Name => "my_tool";
	public string Description => "Does something useful.";
	public JsonElement ParametersSchema => ToolSchema.FromJson("""
		{
			"type": "object",
			"properties": {
				"input": { "type": "string", "description": "The input value" }
			},
			"required": ["input"]
		}
		""");

	public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
	{
		var input = arguments.GetProperty("input").GetString();
		// ... do work ...
		return ToolResult.Ok("result");
	}
}
```

2. Register it in `Program.cs`:

```csharp
registry.Register(new MyTool());
```

That's it — the planner will automatically see the new tool in its prompt.

## Adding an MCP Server

1. Add an entry to `mcp-servers.json`:

```json
{
  "servers": [
	{
	  "name": "my-server",
	  "type": "stdio",
	  "command": "npx",
	  "args": ["-y", "@my/mcp-server"],
	  "timeoutSeconds": 30
	}
  ]
}
```

2. The `McpToolLoader` will discover tools at startup and register them as `mcp_my-server_<tool_name>`.

### MCP Configuration Options

| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | Unique server identifier |
| `type` | No | Transport type: `stdio` (default), `sse`, `http`, `streamablehttp` |
| `command` | stdio only | Command to launch the server process |
| `args` | No | Command-line arguments |
| `env` | No | Environment variables for the process |
| `url` | http/sse only | Server endpoint URL |
| `timeoutSeconds` | No | Connection timeout (default: 30) |

## Security

### File System Sandboxing

`FileSystemSecurity` restricts all file operations to the current working directory tree. Any path that resolves outside this boundary is rejected. This prevents the agent from reading or writing arbitrary files.

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| JSON protocol over native function calling | Works with any model, no special fine-tuning needed |
| Single-project layout | Keeps the codebase simple and easy to understand |
| No dependency injection | Minimal overhead for a CLI tool |
| `MaxSteps` limit | Prevents infinite loops if the model never reaches a final answer |
| MCP as optional layer | Tools can be local or remote without changing the core loop |

## Future Directions

- Conversation memory / persistence
- Streaming responses
- Shell / command execution tool
- Vector search for long documents
- Multi-agent collaboration
