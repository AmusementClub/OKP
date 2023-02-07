using CommandLine;
using OKP.Core.Interface;
using OKP.Core.Interface.Bangumi;
using OKP.Core.Interface.Dmhy;
using OKP.Core.Interface.Nyaa;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using OKP.Core.Utils;
using OKP.Core.Interface.Acgrip;
using OKP.Core.Interface.Acgnx;
using Constants = OKP.Core.Utils.Constants;
using System.Net.Http;

namespace OKP.Core
{
    internal class Program
    {
#pragma warning disable CS8618

        private class Options
        {
            [Value(0, Min = 1, Required = true, MetaName = "torrent", HelpText = "Torrents to be published.(Or Cookie file exported by Get Cookies.txt.)")]
            public IEnumerable<string>? TorrentFile { get; set; }
            [Option('s', "setting", Required = false, Default = Constants.DefaultSettingFileName, HelpText = "(Not required) Specific setting file.")]
            public string SettingFile { get; set; }
            [Option('l', "log_level", Default = "Debug", HelpText = "Log level.")]
            public string? LogLevel { get; set; }
            [Option("log_file", Required = false, Default = Constants.DefaultLogFileName, HelpText = "Log file.")]
            public string LogFile { get; set; }
            [Option('y', HelpText = "Skip reaction.")]
            public bool NoReaction { get; set; }
        }
#pragma warning restore CS8618

        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed(o =>
                   {
                       var levelSwitch = new LoggingLevelSwitch
                       {
                           MinimumLevel = o.LogLevel?.ToLower() switch
                           {
                               "verbose" => LogEventLevel.Verbose,
                               "debug" => LogEventLevel.Debug,
                               "info" => LogEventLevel.Information,
                               _ => LogEventLevel.Debug
                           }
                       };
                       Log.Logger = new LoggerConfiguration()
                       .MinimumLevel.ControlledBy(levelSwitch)
                       .WriteTo.Console()
                       .WriteTo.File(o.LogFile,
                           rollingInterval: RollingInterval.Month,
                           rollOnFileSizeLimit: true)
                       .CreateLogger();
                       IOHelper.NoReaction = o.NoReaction;
                       if (o.TorrentFile is null)
                       {
                           Log.Fatal("o.TorrentFile is null");
                           return;
                       }
                       foreach (var file in o.TorrentFile)
                       {
                           if (!File.Exists(file))
                           {
                               Log.Error("文件{file}不存在", file);
                               continue;
                           }
                           if (file.EndsWith(".torrent"))
                           {
                               Log.Information("正在发布 {File}", file);
                               SinglePublish(file, o.SettingFile);
                           }
                           else if(file.EndsWith(".txt"))
                           {
                               if (File.Exists("cookie.txt"))
                               {
                                   HttpHelper.GlobalCookieContainer.LoadFromTxt("cookie.txt");
                               }
                               Log.Information("正在添加Cookie文件{File}", file);
                               AddCookies(file);
                               HttpHelper.GlobalCookieContainer.SaveToTxt("cookie.txt");
                               Log.Information("Cookie文件{File}添加完成", file);
                               IOHelper.ReadLine();
                           }
                           else
                           {
                               Log.Error("不受支持的文件格式{file}", file);
                           }
                       }
                   });
        }
        private static void SinglePublish(string file, string settingFile)
        {
            if (!File.Exists(file))
            {
                Log.Error("文件不存在！{File}", file);
                IOHelper.ReadLine();
                return;
            }
            var torrent = TorrentContent.Build(file, settingFile, AppDomain.CurrentDomain.BaseDirectory);
            if (torrent.IsV2())
            {
                Log.Error("V2达咩！回去换！");
                IOHelper.ReadLine();
                return;
            }
            torrent.DisplayFileTree();
            List<AdapterBase> adapterList = new();
            if (torrent.IntroTemplate is null)
            {
                Log.Error("没有配置发布站你发个啥？");
                IOHelper.ReadLine();
                return;
            }
            Log.Information("即将发布");
            foreach (var site in torrent.IntroTemplate)
            {
                if (site.Site is null)
                {
                    Log.Error("没有配置发布站你发个啥？");
                    IOHelper.ReadLine();
                    return;
                }
                Log.Information("site: {Site}", site.Site);
                AdapterBase adapter = site.Site.ToLower().Replace(".", "") switch
                {
                    "dmhy" => new DmhyAdapter(torrent, site),
                    "bangumi" => new BangumiAdapter(torrent, site),
                    "nyaa" => new NyaaAdapter(torrent, site),
                    "acgrip" => new AcgripAdapter(torrent, site),
                    "acgnx_asia" => new AcgnxAdapter(torrent, site, "asia"),
                    "acgnx_global" => new AcgnxAdapter(torrent, site, "global"),
                    _ => throw new NotImplementedException()
                };
                adapterList.Add(adapter);
            }
            List<Task<HttpResult>> pingTask = new();
            adapterList.ForEach(p => pingTask.Add(p.PingAsync()));
            var pingRes = Task.WhenAll(pingTask).Result;
            foreach (var res in pingRes)
            {
                if (!res.IsSuccess)
                {
                    Log.Debug("Code: {Code}\tMessage: {Message}", res.Code, res.Message);
                    IOHelper.ReadLine();
                    return;
                }
            }
            Log.Information("登录成功，继续发布？");
            IOHelper.ReadLine();
            foreach (var result in adapterList.Select(item => item.PostAsync().Result))
            {
                if (result.IsSuccess)
                {
                    Log.Information("发布成功");
                }
                else
                {
                    Log.Error("发布失败");
                }
            }
            Log.Information("发布完成");
            HttpHelper.GlobalCookieContainer.SaveToTxt(torrent.CookiePath ?? "cookie.txt");
            IOHelper.ReadLine();
        }
        private static void AddCookies(string file)
        {
            var content = File.ReadAllLines(file);
            foreach (var line in content)
            {
                if (line.StartsWith('#') || line.Length == 0)
                {
                    continue;
                }
                var cookie = line.Split('\t');
                Log.Debug("{domain}:{cookies}", $"https://{cookie[0].TrimStart('.')}",
                    $"{cookie[5]}={cookie[6]}; " +
                    $"expires={UnixTimeToDateTime(long.Parse(cookie[4])):R}; " +
                    $"path={cookie[2]}" +
                    $"{(cookie[3].ToLower() == "ture" ? "; secure" : "")}");
                HttpHelper.GlobalCookieContainer.SetCookies(new($"https://{cookie[0].TrimStart('.')}"),
                    $"{cookie[5]}={cookie[6]}; " +
                    $"expires={UnixTimeToDateTime(long.Parse(cookie[4])):R}; " +
                    $"path={cookie[2]}" +
                    $"{(cookie[3].ToLower() == "ture" ? "; secure" : "")}");
            }
        }
        /// <summary>
        /// Convert Unix time value to a DateTime object.
        /// </summary>
        /// <param name="unixtime">The Unix time stamp you want to convert to DateTime.</param>
        /// <returns>Returns a DateTime object that represents value of the Unix time.</returns>
        private static DateTime UnixTimeToDateTime(long unixtime)
        {
            System.DateTime dtDateTime = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixtime).ToLocalTime();
            return dtDateTime;
        }
    }
}
