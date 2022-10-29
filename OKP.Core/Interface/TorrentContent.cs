using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tomlyn;

namespace OKP.Core.Interface
{
    public class TorrentContent
    {
        public class TorrentData
        {
            public FileInfo FileInfo;
            public ByteArrayContent ByteArrayContent;
            public Torrent TorrentObject;
            public TorrentData(string filename)
            {
                FileInfo = new FileInfo(filename);
                byte[] bytes = File.ReadAllBytes(filename);
                ByteArrayContent = new ByteArrayContent(bytes);
                ByteArrayContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-bittorrent");
                var parser = new BencodeParser(); // Default encoding is Encoding.UTF8, but you can specify another if you need to
                TorrentObject = parser.Parse<Torrent>(filename);
            }
        }
        public TorrentData? Data;

        public List<Template>? IntroTemplate { get; set; }
        public string? DisplayName { get; set; }
        public string? GroupName { get; set; }
        public string? Poster { get; set; }
        public string? About { get; set; }
        public string? FilenameRegex { get; set; }
        public string? ResolutionRegex { get; set; }
        public string? SettingPath { get; set; }
        public bool HasSubtitle { get; set; }
        public bool IsFinished { get; set; }
        public class Template
        {
            public string? Site { get; set; }
            public string? Name { get; set; }
            public string? Content { get; set; }
            public string? Cookie { get; set; }
            public string? UserAgent { get; set; }
            public string? Proxy { get; set; }
        }
        public static TorrentContent Build(string filename, string settingFile, string appLocation)
        {
            var settingFilePath = settingFile;
            if (Path.GetDirectoryName(settingFile) == "")
            {
                settingFilePath = Path.Combine(Path.GetDirectoryName(filename) ?? "", settingFile);
            }

            if (!File.Exists(settingFilePath))
            {
                Log.Error("没有配置文件");
                Console.ReadKey();
                throw new IOException();
            }

            var torrentC = Toml.ToModel<TorrentContent>(File.ReadAllText(settingFilePath));
            torrentC.SettingPath = Path.GetDirectoryName(settingFilePath);
            if (torrentC.DisplayName is null)
            {
                Log.Error("没有配置标题");
                Console.ReadKey();
                throw new IOException();
            }
            torrentC.Data = new(filename);
            if (torrentC.DisplayName.Contains(@"<ep>") && torrentC.FilenameRegex != null && torrentC.FilenameRegex.Contains("(?<ep>"))
            {
                Regex epRegex = new(torrentC.FilenameRegex);
                var epMatch = epRegex.Match(filename);
                if (epMatch.Success)
                {
                    torrentC.DisplayName = torrentC.DisplayName.Replace("<ep>", epMatch.Groups["ep"].Value);
                }
                else
                {
                    Log.Error("标题集数替换失败");
                    Console.ReadKey();
                    throw new IOException();
                }
            }
            if (torrentC.DisplayName.Contains(@"<res>") && torrentC.ResolutionRegex != null && torrentC.ResolutionRegex.Contains("(?<res>"))
            {
                Regex resRegex = new(torrentC.ResolutionRegex);
                var resMatch = resRegex.Match(filename);
                if (resMatch.Success)
                {
                    torrentC.DisplayName = torrentC.DisplayName.Replace("<res>", resMatch.Groups["res"].Value);
                }
                else
                {
                    Log.Error("标题分辨率替换失败");
                    Console.ReadKey();
                    throw new IOException();
                }
            }
            Log.Information("标题：{0}", torrentC.DisplayName);

            /// user properties, it will overlap some existing private config from setting config, such as proxy, cookie and user_agent
            var userPropPath = Path.Combine(appLocation, "OKP_userprop.toml");
            if (File.Exists(userPropPath))
            {
                var userProp = Toml.ToModel<UserProperties>(File.ReadAllText(userPropPath));

                foreach (var p in userProp.UserProp)
                {
                    if (torrentC.IntroTemplate is not null)
                    {
                        foreach (var tp in torrentC.IntroTemplate)
                        {
                            if (p.Name == tp.Name && p.Site == tp.Site)
                            {
                                tp.Proxy = p.Proxy ?? tp.Proxy;
                                tp.Cookie = p.Cookie ?? tp.Cookie;
                                tp.UserAgent = p.UserAgent ?? tp.UserAgent;
                            }
                        }
                    }
                }
            }

            return torrentC;
        }
        public bool IsV2()
        {
            if (Data?.TorrentObject is null)
            {
                Log.Fatal("Data?.TorrentObject is null");
                throw new ArgumentNullException(nameof(Data.TorrentObject));
            }
            if (Data.TorrentObject.ExtraFields["info"] is BDictionary infoValue)
            {
                if (infoValue["meta version"] is BNumber versionValue)
                {
                    if (versionValue == 2)
                    {
                        Log.Verbose("{@TorrentObject}", Data.TorrentObject);
                        return true;
                    }
                }
            }
            return false;
        }

        public void DisplayFiles()
        {
            if (Data?.TorrentObject is null)
            {
                Log.Fatal("Data?.TorrentObject is null");
                throw new ArgumentNullException(nameof(Data.TorrentObject));
            }
            StringBuilder fileList = new();

            if (Data.TorrentObject.FileMode == TorrentFileMode.Multi)
            {
                foreach (var file in Data.TorrentObject.Files)
                {
                    fileList.AppendLine(file.FullPath);
                }
            }
            else
            {
                fileList.AppendLine(Data.TorrentObject.File.FileName);
            }
            Log.Information("文件列表：{FileList}", fileList);
        }
    }

    public class UserProperties
    {
        public List<TorrentContent.Template> UserProp { get; set; }
    }
}
