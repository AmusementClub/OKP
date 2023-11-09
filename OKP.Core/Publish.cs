using OKP.Core.Interface.Acgnx;
using OKP.Core.Interface.Acgrip;
using OKP.Core.Interface.Bangumi;
using OKP.Core.Interface.Dmhy;
using OKP.Core.Interface.Nyaa;
using OKP.Core.Interface;
using OKP.Core.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OKP.Core
{
    internal class Publish
    {
        public static void SinglePublish(string file, string settingFile, string? cookies)
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
        public static void AddCookies(string file)
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
            System.DateTime dtDateTime = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixtime).ToLocalTime();
            return dtDateTime;
        }
    }
}
