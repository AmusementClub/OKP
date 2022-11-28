using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using OKP.Core.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
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
        public static TorrentContent Build(string filename, string settingFile, string appLocation, string userProps)
        {
            var settingFilePath = settingFile;
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
            //Replace user props
            var userPropList = Toml.ToModel<Dictionary<string, string>>(File.ReadAllText(userProps));
            var settingText = File.ReadAllText(settingFilePath);
            settingText = Regex.Replace(settingText, @"#.*", ""); // remove comments
            var matches = Regex.Matches(settingText, @"\{(\w+)\}");
            var misMatch = new List<string>();
            foreach (var match in matches.Cast<Match>())
            {
                if (userPropList.TryGetValue(match.Groups[1].Value, out var prop))
                {
                    settingText = settingText.Replace(match.Value, prop);
                }
                else
                {
                    misMatch.Add(match.Value);
                }
            }
            if (misMatch.Count > 0)
            {
                Log.Error("存在未匹配的UserProps");
                foreach (var item in misMatch)
                {
                    Log.Error("Missing prop: {Prop}", item);
                }
                IOHelper.ReadLine();
                throw new IOException();
            }
            Log.Verbose("Setting file content:{settingText}", settingText);
            var torrentC = Toml.ToModel<TorrentContent>(settingText);
            torrentC.SettingPath = Path.GetDirectoryName(settingFilePath);
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

        public void DisplayFileTree()
        {
            if (Data?.TorrentObject is null)
            {
                Log.Fatal("Data?.TorrentObject is null");
                throw new ArgumentNullException(nameof(Data.TorrentObject));
            }
            StringBuilder fileList = new();

            Node rootNode = Data.TorrentObject.FileMode == TorrentFileMode.Multi ? new Node(Data.TorrentObject.Files) : new Node(Data.TorrentObject.File);
            foreach (var line in Node.GetFileTree(rootNode))
            {
                fileList.AppendLine(line);
            }
            Log.Information("文件列表：{NewLine}{FileList}", Environment.NewLine, fileList);
        }
    }

    public class UserProperties
    {
        public List<TorrentContent.Template>? UserProp { get; set; }
    }
}
