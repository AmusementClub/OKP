using System.CommandLine;
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
using Constants = OKP.Core.Utils.Constants;

namespace OKP.Core
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var torrentArgument = new CliArgument<IEnumerable<string>?>("torrent")
            {
                Description = "Torrents to be published. (Or Cookie file exported by Get Cookies.txt.)"
            };

            var cookiesOption = new CliOption<string?>("--cookies")
            {
                DefaultValueFactory = _ => null,
                Description = "Cookie file to be used."
            };

            var settingOption = new CliOption<string>("--setting", "-s")
            {
                DefaultValueFactory = _ => Constants.DefaultSettingFileName,
                Description = "(Not required) Specific setting file."
            };

            var logLevelOption = new CliOption<string>("--log_level", "-l")
            {
                DefaultValueFactory = _ => "Debug",
                Description = "Log level."
            };

            var logFileOption = new CliOption<string>("--log_file")
            {
                DefaultValueFactory = _ => Constants.DefaultLogFileName,
                Description = "Log file."
            };

            var noReactionOption = new CliOption<bool>("--no_reaction", "-y")
            {
                Description = "Skip reaction."
            };

            var allowSkipOption = new CliOption<bool>("--allow_skip")
            {
                Description = "Ignore login fail and continue publishing."
            };

            var baseTemplateOption = new CliOption<string?>("--base_template", "-b")
            {
                DefaultValueFactory = _ => null,
                Description =
                    "Base template. It needs to be a markdown file, and other site templates will be generated based on it which missing publishing content."
            };

            var rootCommand = new CliRootCommand("One Key Publish")
            {
                torrentArgument,
                cookiesOption,
                settingOption,
                logLevelOption,
                logFileOption,
                noReactionOption,
                allowSkipOption,
                baseTemplateOption
            };

            rootCommand.SetAction((result, _) =>
            {
                var torrentFile = result.GetValue(torrentArgument);
                var cookies = result.GetValue(cookiesOption);
                var settingFile = result.GetValue(settingOption);
                var logLevel = result.GetValue(logLevelOption);
                var logFile = result.GetValue(logFileOption);
                var noReaction = result.GetValue(noReactionOption);
                var allowSkip = result.GetValue(allowSkipOption);
                var baseTemplate = result.GetValue(baseTemplateOption);

                ActionHandler(torrentFile, cookies, settingFile!, logLevel!, logFile!, noReaction, allowSkip, baseTemplate);
                return Task.CompletedTask;
            });

            rootCommand.Parse(args).Invoke();
            IOHelper.ReadLine();
        }

        private static void ActionHandler(IEnumerable<string>? torrentFile, string? cookies, string settingFile, string logLevel, string logFile, bool noReaction, bool allowSkip, string? baseTemplate)
        {
            var levelSwitch = new LoggingLevelSwitch
            {
                MinimumLevel = logLevel.ToLower() switch
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

            if (!string.IsNullOrEmpty(baseTemplate) && Path.GetExtension(baseTemplate) != ".md")
            {
                Log.Fatal("base_template must be a .md file if specified");
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
                var extension = (Path.GetExtension(file) ?? "").AsSpan();

                if (extension.Equals(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("正在发布 {File}", file);
                    SinglePublish(file, settingFile, cookies, allowSkip, baseTemplate);
                    continue;
                }
                if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
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
        }

        private static void SinglePublish(string file, string settingFile, string? cookies, bool allowSkip, string? baseTemplate)
        {
            if (!File.Exists(file))
            {
                Log.Error("文件不存在！{File}", file);
                IOHelper.ReadLine();
                return;
            }
            var torrent = TorrentContent.Build(file, settingFile, baseTemplate, AppDomain.CurrentDomain.BaseDirectory);
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

            var pingTasks = adapterList.Select(async adapter =>
            {
                var result = await adapter.PingAsync();
                return (adapter, httpResult: result);
            }).ToArray();

            var pingRes = Task.WhenAll(pingTasks).GetAwaiter().GetResult();

            foreach (var res in pingRes)
            {
                if (res.httpResult.IsSuccess) continue;

                if (allowSkip)
                {
                    adapterList.Remove(res.adapter);
                }
                else
                {
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
