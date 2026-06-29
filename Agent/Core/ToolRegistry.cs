namespace LocalAgent.Agent.Core;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, IAgentTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IAgentTool> Tools => _tools.Values;

    public void Register(IAgentTool tool)
    {
        if (_tools.ContainsKey(tool.Name))
            throw new InvalidOperationException($"A tool named '{tool.Name}' is already registered.");

        _tools[tool.Name] = tool;
    }

    public bool TryGet(string name, out IAgentTool? tool) => _tools.TryGetValue(name, out tool);
}
