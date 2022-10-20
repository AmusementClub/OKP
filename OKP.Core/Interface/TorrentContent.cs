using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OKP.Core.Interface
{
    internal class TorrentContent
    {
        public FileInfo? FileInfo;
        public ByteArrayContent? ByteArrayContent;
        public Torrent? TorrentObject;
        
        public Template[]? IntroTemplate { get; set; }
        public string? DisplayName { get; set; }
        public string? GroupName { get; set; }
        public string? Poster { get; set; }
        public string? About { get; set; }
        public string? FilenameRegex { get; set; }
        public AnimeType Type { get; set; }
        public class Template
        {
            public string? Site { get; set; }
            public string? Content { get; set; }
            public string? Cookie { get; set; }
            public string? UserAgent { get; set; }
        }
        public enum AnimeType
        {
            Raw,
            BDRip,
            BDRipWithSub,
            WebRip,
            WebRipWithSub
        }
        public TorrentContent(string filename)
        {
            var settingFilePath = Path.Combine(Path.GetDirectoryName(filename) ?? "", "setting.toml");
            if (!File.Exists(settingFilePath))
            {
                Console.WriteLine("没有配置文件");
                Console.ReadKey();
                throw new IOException();
            }
            FileInfo = new FileInfo(filename);
            byte[] bytes = File.ReadAllBytes(filename);
            ByteArrayContent = new ByteArrayContent(bytes);
            ByteArrayContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-bittorrent");
            var parser = new BencodeParser(); // Default encoding is Encoding.UTF8, but you can specify another if you need to
            TorrentObject = parser.Parse<Torrent>(filename);
        }
        public bool isV2()
        {
            if (TorrentObject is null)
            {
                throw new ArgumentNullException(nameof(TorrentObject));
            }
            if (TorrentObject.ExtraFields["info"] is BDictionary infoValue)
            {
                if (infoValue["meta version"] is BNumber versionValue)
                {
                    if (versionValue == 2)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void DisplayFiles()
        {
            if (TorrentObject is null)
            {
                throw new ArgumentNullException(nameof(TorrentObject));
            }
            if (TorrentObject.FileMode == TorrentFileMode.Multi)
            {
                foreach (var file in TorrentObject.Files)
                {
                    Console.WriteLine(file.FullPath);
                }
            }
        }
    }
}
