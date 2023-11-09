using CommandLine;
using OKP.Core.Interface;
using OKP.Core.Interface.Acgnx;
using OKP.Core.Interface.Acgrip;
using OKP.Core.Interface.Bangumi;
using OKP.Core.Interface.Dmhy;
using OKP.Core.Interface.Nyaa;
using OKP.Core.Server;
using OKP.Core.Utils;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Constants = OKP.Core.Utils.Constants;

namespace OKP.Core
{
    internal class Program
    {
#pragma warning disable CS8618

        public class Options
        {
            [Value(0, Min = 1, Required = true, MetaName = "torrent", HelpText = "Torrents to be published.(Or Cookie file exported by Get Cookies.txt.)")]
            public IEnumerable<string>? TorrentFile { get; set; }
            [Option("cookies", Required = false, Default = null, HelpText = "Cookie file to be used.")]
            public string? Cookies { get; set; }
            [Option('s', "setting", Required = false, Default = Constants.DefaultSettingFileName, HelpText = "(Not required) Specific setting file.")]
            public string SettingFile { get; set; }
            [Option('l', "log_level", Default = "Debug", HelpText = "Log level.")]
            public string? LogLevel { get; set; }
            [Option("log_file", Required = false, Default = Constants.DefaultLogFileName, HelpText = "Log file.")]
            public string LogFile { get; set; }
            [Option('y', HelpText = "Skip reaction.")]
            public bool NoReaction { get; set; }
            [Option("server",HelpText ="Server mode.")]
            public bool isServerMode { get; set; }
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
                       Standalone.LocalRun(o);
                   });
            IOHelper.ReadLine();
        }
    }
}
