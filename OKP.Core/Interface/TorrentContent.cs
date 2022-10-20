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
        public void Init(string filename)
        {
            FileInfo = new FileInfo(filename);
            byte[] bytes = File.ReadAllBytes(filename);
            ByteArrayContent = new ByteArrayContent(bytes);
            ByteArrayContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-bittorrent");
            var parser = new BencodeParser(); // Default encoding is Encoding.UTF8, but you can specify another if you need to
            TorrentObject = parser.Parse<Torrent>(filename);
        }
    }
}
