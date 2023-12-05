using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using OKP.Core.Interface;
using OKP.Core.Interface.Acgnx;
using OKP.Core.Interface.Acgrip;
using OKP.Core.Interface.Bangumi;
using OKP.Core.Interface.Dmhy;
using OKP.Core.Interface.Nyaa;
using OKP.Core.Utils;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Spectre.Console;
using Constants = OKP.Core.Utils.Constants;

namespace OKP.Core
{
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class Program
    {
        public static void Main(string[] args)
        {
            var torrentArgument = new Argument<IEnumerable<string>?>(
                name: "torrent",
                description: "Torrents to be published. (Or Cookie file exported by Get Cookies.txt.)"
            );

            var cookiesOption = new Option<string?>(
                name: "--cookies",
                getDefaultValue: () => null,
                description: "Cookie file to be used."
            );

            var settingOption = new Option<string>(
                name: "--setting",
                getDefaultValue: () => Constants.DefaultSettingFileName,
                description: "(Not required) Specific setting file."
            );
            settingOption.AddAlias("-s");

            var logLevelOption = new Option<string?>(
                name: "--log_level",
                getDefaultValue: () => "Debug",
                description: "Log level."
            );
            logLevelOption.AddAlias("-l");

            var logFileOption = new Option<string>(
                name: "--log_file",
                getDefaultValue: () => Constants.DefaultLogFileName,
                description: "Log file."
            );

            var noReactionOption = new Option<bool>(
                name: "--no_reaction",
                description: "Skip reaction."
            );
            noReactionOption.AddAlias("-y");

            var allowSkipOption = new Option<bool>(
                name: "--allow_skip",
                description: "Ignore login fail and continue publishing."
            );

            var rootCommand = new RootCommand("One Key Publish")
            {
                torrentArgument,
                cookiesOption,
                settingOption,
                logLevelOption,
                logFileOption,
                noReactionOption,
                allowSkipOption,
            };

            rootCommand.SetHandler((
                    torrentFile,
                    cookies,
                    settingFile,
                    logLevel,
                    logFile,
                    noReaction,
                    allowSkip) =>
                {
                    var levelSwitch = new LoggingLevelSwitch
                    {
                        MinimumLevel = logLevel?.ToLower() switch
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
                                 .WriteTo.File(logFile,
                                     rollingInterval: RollingInterval.Month,
                                     rollOnFileSizeLimit: true)
                                 .CreateLogger();
                    IOHelper.NoReaction = noReaction;
                    if (torrentFile is null)
                    {
                        Log.Fatal("o.TorrentFile is null");
                        return;
                    }
                    var addCookieCount = 0;
                    foreach (var file in torrentFile)
                    {
                        if (!File.Exists(file))
                        {
                            Log.Error("文件{File}不存在", file);
                            continue;
                        }
                        var extension = (Path.GetExtension(file) ?? "").ToLower();

                        if (extension == ".torrent")
                        {
                            Log.Information("正在发布 {File}", file);
                            SinglePublish(file, settingFile, cookies, allowSkip);
                            continue;
                        }
                        if (extension == ".txt")
                        {
                            if (cookies is null)
                            {
                                Log.Information("请输入Cookie文件名，不需要包含扩展名，相对目录默认为{DefaultPath}",
                                    IOHelper.BasePath(Constants.DefaultCookiePath));
                                IOHelper.HintText(Constants.DefaultCookieFile);
                                var filename = IOHelper.ReadLine();
                                if (File.Exists(filename))
                                {
                                    cookies = filename;
                                    Log.Error("你指定的Cookie文件{File}已经存在！继续添加可能会覆盖之前保存的Cookie！", cookies);
                                    IOHelper.ReadLine();
                                    HttpHelper.GlobalCookieContainer.LoadFromTxt(cookies);
                                }
                                else
                                {
                                    if (!Directory.Exists(IOHelper.BasePath(Constants.DefaultCookiePath)))
                                    {
                                        Directory.CreateDirectory(IOHelper.BasePath(Constants.DefaultCookiePath));
                                    }
                                    cookies = IOHelper.BasePath(Constants.DefaultCookiePath,
                                        (filename?.Length == 0 ? Constants.DefaultCookieFile : filename) + ".txt");
                                    if (File.Exists(cookies))
                                    {
                                        Log.Error("你指定的Cookie文件{File}已经存在！继续添加可能会覆盖之前保存的Cookie！", cookies);
                                        IOHelper.ReadLine();
                                        HttpHelper.GlobalCookieContainer.LoadFromTxt(cookies);
                                    }
                                }
                                Log.Information("请输入你使用的浏览器UserAgent：");
                                var ua = IOHelper.ReadLine();
                                while (ua is null || !HttpHelper.UaRegex.IsMatch(ua))
                                {
                                    Log.Information("你必须输入一个合法的UserAgent以确保你的cookie可以正常使用：");
                                    ua = IOHelper.ReadLine();
                                }
                                HttpHelper.GlobalUserAgent = ua;
                            }
                            if (File.Exists(IOHelper.BasePath(Constants.DefaultCookiePath,
                                    Constants.DefaultCookieFile + ".txt")))
                            {
                                HttpHelper.GlobalCookieContainer.LoadFromTxt(IOHelper.BasePath(Constants.DefaultCookiePath,
                                    Constants.DefaultCookieFile + ".txt"));
                            }
                            Log.Information("正在添加Cookie文件{File}", file);
                            AddCookies(file);
                            addCookieCount++;
                            Log.Information("Cookie文件{File}添加完成，按回车键继续添加", file);
                            IOHelper.ReadLine();
                        }
                        else
                        {
                            Log.Error("不受支持的文件格式{File}", file);
                        }
                        if (cookies is not null)
                        {
                            Log.Information("共输入了{Count}个Cookie文件", addCookieCount);
                            HttpHelper.GlobalCookieContainer.SaveToTxt(cookies, HttpHelper.GlobalUserAgent);
                            Log.Information("保存成功，Cookie文件保存在{Path}", cookies);
                        }
                    }
                },
                torrentArgument,
                cookiesOption,
                settingOption,
                logLevelOption,
                logFileOption,
                noReactionOption,
                allowSkipOption);

            var parser = new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .UseHelp(ctx =>
                {
                    ctx.HelpBuilder.CustomizeLayout(
                        _ =>
                            HelpBuilder.Default
                                       .GetLayout()
                                       .Skip(1) // Skip the default command description section.
                                       .Prepend(hc => hc.Output.Write("Copyright (C) 2023 AmusementClub\nReleased under the GNU GPLv3+.\n"))
                                       .Prepend(
                                           _ => AnsiConsole.Write(
                                               new FigletText(rootCommand.Description!))
                                       ));
                })
                .Build();

            parser.Invoke(args);
            IOHelper.ReadLine();
        }
        private static void SinglePublish(string file, string settingFile, string? cookies,bool allowSkip)
        {
            if (!File.Exists(file))
            {
                Log.Error("文件不存在！{File}", file);
                IOHelper.ReadLine();
                return;
            }
            var torrent = TorrentContent.Build(file, settingFile, AppDomain.CurrentDomain.BaseDirectory);
            if (cookies is null)
            {
                if (torrent.CookiePath is not null)
                {
                    if (File.Exists(torrent.CookiePath))
                    {
                        cookies = torrent.CookiePath;
                        Log.Information("在{Setting}中找到Cookie文件{Cookies}", settingFile, cookies);
                    }
                    else
                    {
                        Log.Error("在{Setting}中找到的Cookie文件{Cookies}不存在!", settingFile, torrent.CookiePath);
                        IOHelper.ReadLine();
                        return;
                    }
                }
                else
                {
                    cookies = IOHelper.BasePath(Constants.DefaultCookiePath, Constants.DefaultCookieFile + ".txt");
                    Log.Information("使用默认的Cookie文件{Cookies}", cookies);
                    if (!File.Exists(cookies))
                    {
                        Log.Error("默认的Cookie文件{Cookies}不存在！", cookies);
                        IOHelper.ReadLine();
                        return;
                    }
                }
            }
            else
            {
                if (!File.Exists(cookies))
                {
                    cookies = IOHelper.BasePath(Constants.DefaultCookiePath, cookies);
                    if (!File.Exists(cookies))
                    {
                        Log.Error("你指定了Cookie文件{Cookies}，但是这个文件不存在。", cookies);
                        IOHelper.ReadLine();
                        return;
                    }
                }
                Log.Information("找到Cookie文件{Cookies}", cookies);
            }
            HttpHelper.GlobalCookieContainer.LoadFromTxt(cookies);
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
                    if (!allowSkip)
                    {
                        return;
                    }
                }
            }
            Log.Information("登录成功，继续发布？");
            IOHelper.ReadLine();
            if(allowSkip)
            {
                Parallel.ForEach(adapterList, p =>
                {
                    var res = p.PostAsync().Result;
                    if (res.IsSuccess)
                    {
                        Log.Information("发布成功");
                    }
                    else
                    {
                        Log.Error("发布失败");
                    }
                });
            }
            else
            {
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
            }
            Log.Information("发布完成");
            HttpHelper.GlobalCookieContainer.SaveToTxt(cookies, HttpHelper.GlobalUserAgent);
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
            if (unixtime == 0)
            {
                return new(2099, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            }
            DateTime dtDateTime = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixtime).ToLocalTime();
            return dtDateTime;
        }
    }
}
