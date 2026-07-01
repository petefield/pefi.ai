namespace LocalAgent.Agent.Llm.Ollama;

public sealed partial class OllamaClient
{
    public sealed record OllamaChunk(
        string? Response,
        string? Thinking,
        bool Done
    );
}
