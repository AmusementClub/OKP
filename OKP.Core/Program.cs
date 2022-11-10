using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using CommandLine.Text;
using CommandLine;
using OKP.Core.Interface;
using OKP.Core.Interface.Bangumi;
using OKP.Core.Interface.Dmhy;
using OKP.Core.Interface.Nyaa;
using Serilog;
using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;
using OKP.Core.Utils;
using OKP.Core.Interface.Acgrip;

namespace OKP
{
    internal class Program
    {
        class Options
        {
            [Value(0, Min = 1, Required = true, MetaName = "torrent", HelpText = "Torrents to be published.")]
            public IEnumerable<string>? TorrentFile { get; set; }
            [Option('s', "setting", Required = false, HelpText = "(Not required) Specific setting file.")]
            public string? SettingFile { get; set; }
            [Option('l', "log_level", Default = "Debug", HelpText = "Log level.")]
            public string? LogLevel { get; set; }
            [Option("log_file", Default = "log.txt")]
            public string? LogFile { get; set; }
            [Option('y', HelpText = "Skip reaction.")]
            public bool NoReaction { get; set; }
        }
        static void Main(string[] args)
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
                       .WriteTo.File(o.LogFile ?? "log.txt",
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
                           Log.Information("正在发布{0}", file);
                           SinglePublish(file, o.SettingFile);
                       }
                   });
        }
        static void SinglePublish(string file, string? settingFile)
        {

            if (!File.Exists(file))
            {
                Log.Error("文件不存在！{0}", file);
                IOHelper.ReadLine();
                return;
            }
            settingFile ??= "setting.toml";
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
                Log.Information(site.Site);
                AdapterBase adapter = site.Site.ToLower().Replace(".","") switch
                {
                    "dmhy" => new DmhyAdapter(torrent, site),
                    "bangumi" => new BangumiAdapter(),
                    "nyaa" => new NyaaAdapter(torrent, site),
                    "acgrip"=> new AcgripAdapter(torrent,site),
                    _ => throw new NotImplementedException()
                };
                adapterList.Add(adapter);
            }
            List<Task<HttpResult>> PingTask = new();
            adapterList.ForEach(p => PingTask.Add(p.PingAsync()));
            var PingRes = Task.WhenAll(PingTask).Result;
            foreach (var res in PingRes)
            {
                if (!res.IsSuccess)
                {
                    Log.Debug(res.Code + "\t" + res.Message);
                    IOHelper.ReadLine();
                    return;
                }
            }
            Log.Information("登录成功，继续发布？");
            IOHelper.ReadLine();
            foreach (var item in adapterList)
            {
                var result = item.PostAsync().Result;
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
            IOHelper.ReadLine();
        }
    }
}