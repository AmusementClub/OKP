using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OKP.Core.Utils
{
    internal class FileHelper
    {
        public static string? ParseFileFullPath(string file, string? rootFile)
        {
            string fullPath = "";
            if (file.StartsWith("\\\\") || file.Contains(":\\"))
            {
                fullPath = file;
            }
            else
            {
                var root = Directory.Exists(rootFile) ? rootFile : Path.GetDirectoryName(rootFile);
                if (root != null)
                {
                    fullPath = Path.Combine(root, file);
                }
            }
            return fullPath;
        }
    }
}
