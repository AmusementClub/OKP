using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OKP.Core.Interface
{
    internal class TorrentContent
    {
        public FileInfo? FileInfo;
        public string? Template { get; set; }
        public string? DisplayName { get; set; }
        public string? GroupName { get; set; }
        public string? Poster { get; set; }
        public string? About { get; set; }
        public string? FilenameRegex { get; set; }
        public AnimeType Type { get; set; }
        public enum AnimeType
        {
            Raw,
            BDRip,
            BDRipWithSub,
            WebRip,
            WebRipWithSub
        }
    }
}
