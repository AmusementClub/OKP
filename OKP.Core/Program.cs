using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;

namespace OKP
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args[0] is null)
            {
                throw new ArgumentNullException(nameof(args));
            }
            var parser = new BencodeParser(); // Default encoding is Encoding.UTF8, but you can specify another if you need to
            var torrent = parser.Parse<Torrent>(args[0]);
            Console.WriteLine(torrent.DisplayName);
            if (torrent.ExtraFields["info"] is BDictionary infoValue)
            {
                if (infoValue["meta version"] is  BNumber versionValue)
                {
                    if (versionValue == 2)
                    {
                        Console.WriteLine("V2达咩！回去换！");
                        Console.ReadLine();
                        return;
                    }
                }
            }
            if (torrent.FileMode == TorrentFileMode.Multi)
            {
                foreach(var file in torrent.Files)
                {
                    Console.WriteLine(file.FullPath);
                }
            }
            foreach(var tracker in torrent.Trackers)
            {
                Console.WriteLine(tracker.Count);
            }
            var settingFilePath = Path.Combine(Path.GetDirectoryName(args[0]) ?? "", "setting.toml");
            if (!File.Exists(settingFilePath))
            {
                Console.WriteLine("没有配置文件");
            }
            var settingList = Directory.GetFiles(Path.GetDirectoryName(args[0]) ?? "","");
        }
    }
}