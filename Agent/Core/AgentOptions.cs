namespace LocalAgent.Agent.Core;

public sealed class AgentOptions
{
    public string Model { get; set; } = "qwen2.5:7b";
    public int MaxSteps { get; set; } = 8;
    public double Temperature { get; set; } = 0.1;
}
