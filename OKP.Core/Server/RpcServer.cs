using OKP.Core.Interface;
using OKP.Core.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OKP.Core.Server
{
    internal class RpcServer
    {
        public class MessageModel
        {
            public int Code { get; set; }
            public object Data { get; set; }
            public MessageModel(int code, object data)
            {
                Code = code;
                Data = data;
            }
        }
        public static MessageModel Ping() => new(200, "Success");
        public static MessageModel CheckSettingFile(string path) => new(File.Exists(path) ? 200 : 404, "");
        public static MessageModel BuildTorrent(string file, string settingFile, string? cookies)
        {
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
                        return new(404, "在{Setting}中找到的Cookie文件{Cookies}不存在!");
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
                        return new(404, "默认的Cookie文件{Cookies}不存在！");
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
                        return new(404, "你指定了Cookie文件{Cookies}，但是这个文件不存在。");
                    }
                }
                Log.Information("找到Cookie文件{Cookies}", cookies);
            }
            HttpHelper.GlobalCookieContainer.LoadFromTxt(cookies);
            return new(200,"");
        }
    }
}
