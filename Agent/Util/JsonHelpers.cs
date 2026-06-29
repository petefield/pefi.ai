namespace LocalAgent.Agent.Util;

public static class JsonHelpers
{
    public static string? ExtractFirstJsonObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return null;

        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (ch == '\\' && inString)
            {
                escape = true;
                continue;
            }

            if (ch == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString) continue;

            if (ch == '{') depth++;
            if (ch == '}') depth--;

            if (depth == 0)
                return text[start..(i + 1)];
        }

        return null;
    }
}
