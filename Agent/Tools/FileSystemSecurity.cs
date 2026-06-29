namespace LocalAgent.Agent.Tools;

public sealed class FileSystemSecurity
{
    public string RootDirectory { get; }

    public FileSystemSecurity(string rootDirectory)
    {
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string ResolveInsideRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            path = ".";

        var candidate = Path.GetFullPath(Path.Combine(RootDirectory, path));

        if (!candidate.StartsWith(RootDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path '{path}' escapes the allowed root directory '{RootDirectory}'.");

        return candidate;
    }
}
