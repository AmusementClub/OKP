using OKP.Core.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OKP.Core.Server
{
    internal class Standalone
    {
        public static void LocalRun(Program.Options o)
        {
            IOHelper.NoReaction = o.NoReaction;
            if (o.TorrentFile is null)
            {
                Log.Fatal("o.TorrentFile is null");
                return;
            }
            int addCookieCount = 0;
            foreach (var file in o.TorrentFile)
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
                    Publish.SinglePublish(file, o.SettingFile, o.Cookies);
                    continue;
                }
                if (extension == ".txt")
                {
                    if (o.Cookies is null)
                    {
                        Log.Information("请输入Cookie文件名，不需要包含扩展名，相对目录默认为{DefaultPath}", IOHelper.BasePath(Constants.DefaultCookiePath));
                        IOHelper.HintText(Constants.DefaultCookieFile);
                        var filename = IOHelper.ReadLine();
                        if (File.Exists(filename))
                        {
                            o.Cookies = filename;
                            Log.Error("你指定的Cookie文件{File}已经存在！继续添加可能会覆盖之前保存的Cookie！", o.Cookies);
                            IOHelper.ReadLine();
                            HttpHelper.GlobalCookieContainer.LoadFromTxt(o.Cookies);
                        }
                        else
                        {
                            if (!Directory.Exists(IOHelper.BasePath(Constants.DefaultCookiePath)))
                            {
                                Directory.CreateDirectory(IOHelper.BasePath(Constants.DefaultCookiePath));
                            }
                            o.Cookies = IOHelper.BasePath(Constants.DefaultCookiePath, (filename?.Length == 0 ? Constants.DefaultCookieFile : filename) + ".txt");
                            if (File.Exists(o.Cookies))
                            {
                                Log.Error("你指定的Cookie文件{File}已经存在！继续添加可能会覆盖之前保存的Cookie！", o.Cookies);
                                IOHelper.ReadLine();
                                HttpHelper.GlobalCookieContainer.LoadFromTxt(o.Cookies);
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
                    if (File.Exists(IOHelper.BasePath(Constants.DefaultCookiePath, Constants.DefaultCookieFile + ".txt")))
                    {
                        HttpHelper.GlobalCookieContainer.LoadFromTxt(IOHelper.BasePath(Constants.DefaultCookiePath, Constants.DefaultCookieFile + ".txt"));
                    }
                    Log.Information("正在添加Cookie文件{File}", file);
                    Publish.AddCookies(file);
                    addCookieCount++;
                    Log.Information("Cookie文件{File}添加完成，按回车键继续添加", file);
                    IOHelper.ReadLine();
                }
                else
                {
                    Log.Error("不受支持的文件格式{File}", file);
                }
                if (o.Cookies is not null)
                {
                    Log.Information("共输入了{Count}个Cookie文件", addCookieCount);
                    HttpHelper.GlobalCookieContainer.SaveToTxt(o.Cookies, HttpHelper.GlobalUserAgent);
                    Log.Information("保存成功，Cookie文件保存在{Path}", o.Cookies);
                }
            }
        }
    }
}
