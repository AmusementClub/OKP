using BencodeNET.Torrents;
using OKP.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static OKP.Core.Interface.TorrentContent;

namespace OKP.Core.Interface.Nyaa
{
    internal class NyaaAdapter : AdapterBase
    {
        private readonly HttpClient httpClient;
        private readonly CookieContainer cookieContainer;
        private readonly Template template;
        private readonly TorrentContent torrent;
        private readonly Regex cookieReg = new(@"session=([a-zA-Z0-9|\.]+)");
        private readonly List<string> trackers = new() { "http://nyaa.tracker.wf:7777/announce" };

        private readonly Uri baseUrl = new("https://nyaa.si/");
        private string pingUrl => "upload";
        private string postUtl => "upload";
        public NyaaAdapter(TorrentContent torrent, Template template)
        {
            cookieContainer = new();
            var httpClientHandler = new HttpClientHandler() { CookieContainer = cookieContainer };
            httpClient = new(httpClientHandler)
            {
                BaseAddress = baseUrl,
            };
            this.template = template;
            this.torrent = torrent;
            if (template == null)
            {
                return;
            }
            if (template.Cookie is null)
            {
                Console.WriteLine("Empty nyaa cookie");
                Console.ReadKey();
                return;
            }
            var match = cookieReg.Match(template.Cookie);
            if (!match.Success)
            {
                Console.WriteLine("Wrong nyaa cookie");
                Console.ReadKey();
                return;
            }
            cookieContainer.Add(new Cookie("session", match.Groups[1].Value, "/", "nyaa.si"));
            if (!Valid())
            {
                Console.ReadLine();
                throw new();
            }
        }

        public override async Task<HttpResult> PingAsync()
        {
            var pingReq = await httpClient.GetAsync(pingUrl);
            var raw = await pingReq.Content.ReadAsStringAsync();
            if (!pingReq.IsSuccessStatusCode)
            {
                return new((int)pingReq.StatusCode, raw, false);
            }
            if (raw.Contains(@"You are not logged in"))
            {
                return new(403, "Login failed" + raw, false);
            }
            foreach (var cookieHeader in pingReq.Headers.GetValues("Set-Cookie"))
            {
                cookieContainer.SetCookies(baseUrl, cookieHeader);
            }
            return new(200, "Success", true);
        }

        public override async Task<HttpResult> PostAsync()
        {
            Console.WriteLine("正在发布nyaa");
            if (torrent.Data is null)
            {
                throw new NotImplementedException();
            }
            MultipartFormDataContent form = new()
            {
                { torrent.Data.ByteArrayContent, "torrent_file", torrent.Data.FileInfo.Name},
                { new StringContent(torrent.DisplayName??""), "display_name" },
                { new StringContent(torrent.HasSubtitle ? "1_3": "1_4"), "category" },
                { new StringContent(torrent.About??""), "information" },
                { new StringContent(template.Content??""), "description" },
            };
            var result = await httpClient.PostAsyncWithRetry(postUtl, form);
            var raw = await result.Content.ReadAsStringAsync();
            if (result.StatusCode == HttpStatusCode.Redirect)
            {
                if (raw.Contains("You should be redirected automatically to target URL"))
                {
                    return new(200, "Success", true);
                }
                else
                {
                    return new(500, "Upload failed" + raw, false);
                }
            }
            else
            {
                return new((int)result.StatusCode, "Failed" + raw, false);
            }
        }

        private bool Valid()
        {
            if (torrent.Data?.TorrentObject is null)
            {
                throw new ArgumentNullException(nameof(torrent.Data.TorrentObject));
            }
            foreach (var tracker in trackers)
            {
                if (!torrent.Data.TorrentObject.Trackers.ToList().Exists(p => p.First().TrimEnd('/').ToLower() == tracker.TrimEnd('/').ToLower()))
                {
                    Console.WriteLine("缺少Tracker：{0}", tracker);
                    return false;
                }
            }
            if (template.Content != null && template.Content.ToLower().EndsWith(".md"))
            {
                var templateFile = FileHelper.ParseFileFullPath(template.Content, torrent.Data.FileInfo.FullName);
                if (File.Exists(templateFile))
                {
                    template.Content = File.ReadAllText(templateFile);
                }
                else
                {
                    Console.WriteLine("发布模板看起来是个.md文件，但是这个.md文件不存在");
                    Console.WriteLine("{0}-->{1}", template.Content, templateFile);
                    return false;
                }
            }
            return true;
        }
    }
}
