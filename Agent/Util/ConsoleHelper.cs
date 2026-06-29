using System;


namespace LocalAgent.Console.Agent.Util
{
    internal static class Con
    {
        public static void WriteLine(string message, ConsoleColor color)
        {
            var col = System.Console.ForegroundColor;
            System.Console.ForegroundColor = color;
            System.Console.WriteLine(message);
            System.Console.ForegroundColor = col;
        }
    }
}
