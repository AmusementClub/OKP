namespace OKP.Core.Utils
{
    internal class FileHelper
    {
        public static string? ParseFileFullPath(string file, string? rootFile)
        {
            var fullPath = "";
            if (Path.IsPathRooted(file))
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
