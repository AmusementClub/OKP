
namespace OKP.Core.Utils
{
    internal static class IOHelper
    {
        public static bool NoReaction = false;
        public static string? ReadLine()
        {
            return NoReaction ? null : Console.ReadLine();
        }
    }
}
