using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OKP.Core.Utils
{
    internal static class IOHelper
    {
        static public bool NoReaction = false;
        public static string? ReadLine()
        {
            return NoReaction?null: Console.ReadLine();
        }
    }
}
