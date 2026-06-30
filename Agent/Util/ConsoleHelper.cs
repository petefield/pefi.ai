using System;
using LocalAgent.Agent.Core;


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

        public static void WriteLineItalic(string message, ConsoleColor color)
        {
            var col = System.Console.ForegroundColor;
            System.Console.ForegroundColor = color;
            System.Console.Write("\x1b[3m"); // ANSI escape code for italic
            System.Console.WriteLine(message);
            System.Console.Write("\x1b[23m"); // ANSI escape code to reset italic
            System.Console.ForegroundColor = col;
        }


        public static void WriteItalic(string message, ConsoleColor color)
        {
            var col = System.Console.ForegroundColor;
            System.Console.ForegroundColor = color;
            System.Console.Write("\x1b[3m"); // ANSI escape code for italic
            System.Console.Write(message);
            System.Console.Write("\x1b[23m"); // ANSI escape code to reset italic
            System.Console.ForegroundColor = col;
        }

        public static void ShowWelcome(string ollamaUrl, AgentOptions options)
        {
            
            //System.Console.Clear();
            
            // ASCII Art Header
            WriteLine(@"╔═══════════════════════════════════════════════════╗", ConsoleColor.Cyan);
            WriteLine(@"║                                                   ║", ConsoleColor.Cyan);
            WriteLine(@"║    ██████╗ ███████╗███████╗██╗      █████╗ ██╗    ║", ConsoleColor.Cyan);
            WriteLine(@"║    ██╔══██╗██╔════╝██╔════╝██║     ██╔══██╗██║    ║", ConsoleColor.Cyan);
            WriteLine(@"║    ██████╔╝█████╗  █████╗  ██║     ███████║██║    ║", ConsoleColor.Cyan);
            WriteLine(@"║    ██╔═══╝ ██╔══╝  ██╔══╝  ██║     ██╔══██║██║    ║", ConsoleColor.Cyan);
            WriteLine(@"║    ██║     ███████╗██║     ██║     ██║  ██║██║    ║", ConsoleColor.Cyan);
            WriteLine(@"║    ╚═╝     ╚══════╝╚═╝     ╚═╝     ╚═╝  ╚═╝╚═╝    ║", ConsoleColor.Cyan);
            WriteLine(@"║                                                   ║", ConsoleColor.Cyan);
            WriteLine(@"║         AI-Powered Local Agent Runtime            ║", ConsoleColor.Cyan);
            WriteLine(@"║                                                   ║", ConsoleColor.Cyan);
            WriteLine(@"╚═══════════════════════════════════════════════════╝", ConsoleColor.Cyan);
            System.Console.WriteLine();
            

            var ConfigHeader = new BoxedMessage("Configuration", 50, ConsoleColor.White, ConsoleColor.Cyan);
            ConfigHeader.Write($"🤖 Ollama URL : {ollamaUrl}");
            ConfigHeader.Write($"📦 Model : {options.Model}");
            ConfigHeader.Write($"🔧 Max Steps  : {options.MaxSteps}");
            ConfigHeader.End();

            System.Console.WriteLine();


            var commandsMessage = new BoxedMessage("Commands", 50, ConsoleColor.White, ConsoleColor.Cyan);
            commandsMessage.Write("/tools           List all registered tools");
            commandsMessage.Write("/model <name>    Switch to a different model");
            commandsMessage.Write("/reset           Clear conversation history");
            commandsMessage.Write("/exit            Quit the application");
            commandsMessage.End();
            
            System.Console.WriteLine();
            
            // Ready prompt
            WriteLine("🚀 Ready! Type your message or use a command to get started...", ConsoleColor.Green);
            System.Console.WriteLine();
        }

        private class BoxedMessage
        {
            private readonly string Title;

            public ConsoleColor TextColor { get; }
            public ConsoleColor BoxColor { get; }

            public int Width { get; } = 50;

            public BoxedMessage(string title, int width, ConsoleColor textColor, ConsoleColor boxColor)
            {
                Title = title;
                Width = width;
                TextColor = textColor;
                BoxColor = boxColor;
                WriteLine($"┌─ {Title} {new string('─', Width - Title.Length - 2)}┐", ConsoleColor.Cyan);
            }

            public void Write(string message)
            {
                var col = System.Console.ForegroundColor;
                System.Console.ForegroundColor = BoxColor;
                System.Console.Write("│");
                System.Console.ForegroundColor = TextColor;

                var line = $"  {message} {new string(' ', Width - message.Length - 3)}";
                System.Console.Write($"{line}");
                System.Console.ForegroundColor = BoxColor;
                System.Console.WriteLine(" │");
                System.Console.ForegroundColor = col;
            }

            public void End()
            {
                WriteLine($"└{new string('─', Width+1)}┘", BoxColor);
            }
        }
    }

    
    
}
