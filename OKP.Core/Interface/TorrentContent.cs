﻿using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using OKP.Core.Utils;
using Serilog;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace OKP.Core.Interface
{
    public class TorrentContent
    {
        public class TorrentData
        {
            public FileInfo FileInfo;
            public ByteArrayContent ByteArrayContent
            {
                //When adding this content into a MultipartFormDataContent, the filename prop will be stored in this ByteArrayContent object, which cannot be overwrite.
                //Use a setter to return a new object everytime.
                get
                {
                    var ByteArrayContent = new ByteArrayContent(bytes);
                    ByteArrayContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-bittorrent");
                    return ByteArrayContent;
                }
            }
            public Torrent TorrentObject;
            private readonly byte[] bytes;
            public TorrentData(string filename)
            {
                FileInfo = new FileInfo(filename);
                bytes = File.ReadAllBytes(filename);
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
        // public bool HasSubtitle { get; set; }
        // public bool IsFinished { get; set; }
        public string? CookiePath { get; set; }
        public List<ContentTypes>? Tags { get; set; }
        public List<NyaaTorrentFlags>? TorrentFlags { get; set; }
        public class Template
        {
            public string? Site { get; set; }
            public string? Name { get; set; }
            public string? Content { get; set; }
            public string? Cookie { get; set; }
            public string? UserAgent { get; set; }
            public string? Proxy { get; set; }
            public string? DisplayName { get; set; }
        }
        public enum ContentTypes
        {
            Anime,
            Music,
            Action,
            Picture,
            Comic,
            Novel,
            Software,
            Others,
            MV,
            Chinese,
            HongKong,
            Taiwan,
            English,
            Japanese,
            TV,
            Movie,
            Batch,    // same as Collection
            Collection,
            Raw,
            Lossless,
            Lossy,
            ACG,
            Doujin,
            Pop,
            Idol,
            Tokusatsu,
            Show,
            Graphics,
            Photo,
            App,
            Game
        }
        public static TorrentContent Build(string filename, string settingFile, string? baseTemplate, string appLocation)
        {
            var settingFilePath = settingFile;

            if (Path.GetDirectoryName(filename) == "")
            {
                filename = Path.Combine(Environment.CurrentDirectory, filename);
            }

            if (Path.GetDirectoryName(settingFile) == "")
            {
                settingFilePath = Path.Combine(Path.GetDirectoryName(filename) ?? "", settingFile);
            }

            if (!File.Exists(settingFilePath))
            {
                Log.Error("没有配置文件");
                IOHelper.ReadLine();
                throw new IOException();
            }

            var torrentC = TomlParseHelper.DeserializeTorrentContent(settingFilePath);
            ProcessTemplate(torrentC, baseTemplate);
            torrentC.SettingPath = Path.GetDirectoryName(settingFilePath);
            //if (!File.Exists(torrentC.CookiePath))
            //{
            //    Log.Error("没有设置Cookie,使用默认Cookie文件{file}",Path.GetFullPath("cookie.txt"));
            //    torrentC.CookiePath = "cookie.txt";
            //}
            //HttpHelper.GlobalCookieContainer.LoadFromTxt(torrentC.CookiePath);
            if (torrentC.DisplayName is null)
            {
                Log.Error("没有配置标题");
                IOHelper.ReadLine();
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
                    IOHelper.ReadLine();
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
                    IOHelper.ReadLine();
                    throw new IOException();
                }
            }
            Log.Information("标题：{0}", torrentC.DisplayName);

            if (torrentC.Tags is not null)
            {
                Log.Information("标签：{0}", string.Join(", ", torrentC.Tags));
            }

            // user properties, it will overlap some existing private config from setting config, such as proxy, cookie and user_agent
            var userPropPath = IOHelper.BasePath(Constants.DefaultUserPropsPath, Constants.UserPropertiesFileName);
            if (File.Exists(userPropPath))
            {
                var userProp = TomlParseHelper.DeserializeUserProperties(userPropPath);

                if (userProp.UserProp == null)
                    return torrentC;
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

        private static void ProcessTemplate(TorrentContent torrentC, string? baseTemplate)
        {
            if (baseTemplate is null) return;
            if (torrentC.IntroTemplate is null) return;

            if (Path.GetDirectoryName(baseTemplate) == "")
            {
                baseTemplate = Path.Combine(Environment.CurrentDirectory, baseTemplate);
            }

            // Automatically generate for sites with missing publishing content
            foreach (var site in torrentC.IntroTemplate.Where(site => string.IsNullOrEmpty(site.Content)))
            {
                site.Content = baseTemplate;
            }
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

        public void DisplayFileTree()
        {
            if (Data?.TorrentObject is null)
            {
                Log.Fatal("Data?.TorrentObject is null");
                throw new ArgumentNullException(nameof(Data.TorrentObject));
            }
            StringBuilder fileList = new();

            var rootNode = Data.TorrentObject.FileMode == TorrentFileMode.Multi ? new Node(Data.TorrentObject.Files) : new Node(Data.TorrentObject.File);
            foreach (var line in Node.GetFileTree(rootNode))
            {
                fileList.AppendLine(line);
            }
            Log.Information("文件列表：{NewLine}{FileList}", Environment.NewLine, fileList);
        }

        [Flags]
        public enum NyaaTorrentFlags
        {
            None = 0b_0,
            Anonymous = 0b_1,
            Hidden = 0b_10,
            Remake = 0b_100,
            Complete = 0b_1000,
        }
    }

    public class UserProperties
    {
        public List<TorrentContent.Template>? UserProp { get; set; }
    }
}
