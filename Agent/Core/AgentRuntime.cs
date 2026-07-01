using LocalAgent.Console.Agent.Util;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace LocalAgent.Agent.Core;

public sealed class AgentRuntime
{
    private readonly IAgentPlanner _planner;
    private readonly ToolRegistry _tools;
    private readonly AgentOptions _options;
    private readonly List<AgentMessage> _messages = [];

    public AgentRuntime(IAgentPlanner planner, ToolRegistry tools, AgentOptions options)
    {
        _planner = planner;
        _tools = tools;
        _options = options;
    }

    public IReadOnlyList<AgentMessage> Messages => _messages;

    public void Reset() => _messages.Clear();

    public async Task<string> RunAsync(string userInput, CancellationToken cancellationToken)
    {
        Con.WriteLine($"Thinking...", ConsoleColor.Green);

        _messages.Add(new AgentMessage(AgentRole.User, userInput));

        for (var step = 1; step <= _options.MaxSteps; step++)
        {
            var action = await _planner.GetNextActionAsync(_messages, _tools.Tools, _options, cancellationToken);

            if (action.Type == AgentActionType.Final)
            {
                var content = action.Content ?? string.Empty;
                _messages.Add(new AgentMessage(AgentRole.Assistant, content));
                return content;
            }

            Con.WriteLine(action.ToString(), ConsoleColor.Cyan);


            if (string.IsNullOrWhiteSpace(action.ToolName))
            {
                var error = "The model requested a tool call without a tool name.";
                _messages.Add(new AgentMessage(AgentRole.Tool, error));
                continue;
            }

            if (!_tools.TryGet(action.ToolName, out var tool) || tool is null)
            {
                var error = $"Tool '{action.ToolName}' is not registered.";
                _messages.Add(new AgentMessage(AgentRole.Tool, error));
                continue;
            }

            ToolResult result;
            try
            {
                Con.WriteLine($"Executing tool {tool.Name}...", ConsoleColor.Yellow);
                result = await tool.ExecuteAsync(action.Arguments, cancellationToken);
            }
            catch (Exception ex)
            {
                result = ToolResult.Fail(ex.Message);
            }

            var toolObservation = JsonSerializer.Serialize(new
            {
                type = "tool_result",
                tool = action.ToolName,
                success = result.Success,
                content = result.Content,
                error = result.Error
            }, new JsonSerializerOptions { WriteIndented = true });

            _messages.Add(new AgentMessage(AgentRole.Tool, toolObservation));
        }

        return $"I reached the maximum number of steps ({_options.MaxSteps}) without completing the task.";
    }

    public async IAsyncEnumerable<string> RunStreamingAsync(string userInput, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Con.WriteLineItalic($"Thinking...", ConsoleColor.Green);

        _messages.Add(new AgentMessage(AgentRole.User, userInput));

        for (var step = 1; step <= _options.MaxSteps; step++)
        {
            var action = await _planner.GetNextActionAsync(_messages, _tools.Tools, _options, cancellationToken);

            if (action.Type == AgentActionType.Final)
            {
                System.Console.WriteLine();

                // Stream the final response content (already parsed)
               var actionContent = action.Content ?? string.Empty;
                _messages.Add(new AgentMessage(AgentRole.Assistant, actionContent));
                yield return actionContent;
                yield break;
            }

            if (string.IsNullOrWhiteSpace(action.ToolName))
            {
                var error = "The model requested a tool call without a tool name.";
                _messages.Add(new AgentMessage(AgentRole.Tool, error));
                continue;
            }

            if (!_tools.TryGet(action.ToolName, out var tool) || tool is null)
            {
                var error = $"Tool '{action.ToolName}' is not registered.";
                _messages.Add(new AgentMessage(AgentRole.Tool, error));
                continue;
            }

            ToolResult result;
            try
            {
                Con.WriteItalic($" - Calling tool `{tool.Name}`...", ConsoleColor.Cyan);
                result = await tool.ExecuteAsync(action.Arguments, cancellationToken);
                Con.WriteLineItalic($"✔", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                result = ToolResult.Fail(ex.Message);
                Con.WriteLineItalic($"✖", ConsoleColor.Red);
            }

            var toolObservation = JsonSerializer.Serialize(new
            {
                type = "tool_result",
                tool = action.ToolName,
                success = result.Success,
                content = result.Content,
                error = result.Error
            }, new JsonSerializerOptions { WriteIndented = true });

            _messages.Add(new AgentMessage(AgentRole.Tool, toolObservation));
        }

        yield return $"I reached the maximum number of steps ({_options.MaxSteps}) without completing the task.";
    }
}
