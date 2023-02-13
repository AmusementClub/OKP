﻿
namespace OKP.Core.Utils
{
    internal static class IOHelper
    {
        public static bool NoReaction = false;
        public static string? ReadLine()
        {
            return NoReaction ? "" : Console.ReadLine();
        }

        public static string basePath(string file) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
        public static void HintText(string hint)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(hint);
            Console.SetCursorPosition(Console.CursorLeft - hint.Length, Console.CursorTop);
            Console.ForegroundColor = color;
        }
    }
}
