using BencodeNET.Torrents;
using OKP.Core.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        private readonly Template template;
        private readonly TorrentContent torrent;
        private readonly Regex cookieReg = new(@"session=([a-zA-Z0-9|\.|_|-]+)");
        private readonly List<string> trackers = new() { "http://nyaa.tracker.wf:7777/announce" };

        private readonly Uri baseUrl = new("https://nyaa.si/");
        private readonly string pingUrl = "upload";
        private readonly string postUrl = "upload";
        private string category;
        const string site = "nyaa";
        public NyaaAdapter(TorrentContent torrent, Template template)
        {
            var httpClientHandler = new HttpClientHandler()
            {
                CookieContainer = HttpHelper.GlobalCookieContainer,
                AllowAutoRedirect = false
            };
            httpClient = new(httpClientHandler)
            {
                BaseAddress = baseUrl,
            };
            this.template = template;
            this.torrent = torrent;
            if (template.Proxy is not null)
            {
                httpClientHandler.Proxy = new WebProxy(
                    new Uri(template.Proxy),
                    BypassOnLocal: false);
                httpClientHandler.UseProxy = true;
            }
            category = CategoryHelper.SelectCategory(torrent.Tags, site);
            if (!Valid())
            {
                IOHelper.ReadLine();
                throw new();
            }
        }

        public override async Task<HttpResult> PingAsync()
        {
            var pingReq = await httpClient.GetAsyncWithRetry(pingUrl);
            var raw = await pingReq.Content.ReadAsStringAsync();
            if (!pingReq.IsSuccessStatusCode)
            {
                Log.Error("Cannot connect to {Site}.{NewLine}" +
                    "Code: {Code}{NewLine}" +
                    "Raw: {Raw}", site, Environment.NewLine, pingReq.StatusCode, Environment.NewLine, raw);
                return new((int)pingReq.StatusCode, raw, false);
            }
            if (raw.Contains(@"You are not logged in"))
            {
                Log.Error("{Site} login failed", site);
                return new(403, "Login failed" + raw, false);
            }
            Log.Debug("{Site} login success.", site);
            return new(200, "Success", true);
        }

        public override async Task<HttpResult> PostAsync()
        {
            Log.Information("正在发布nyaa");
            if (torrent.Data is null)
            {
                Log.Fatal("{Site} torrent.Data is null", site);
                throw new NotImplementedException();
            }
            MultipartFormDataContent form = new()
            {
                { torrent.Data.ByteArrayContent, "torrent_file", torrent.Data.FileInfo.Name},
                { new StringContent(torrent.DisplayName??""), "display_name" },
                { new StringContent(category), "category" },
                { new StringContent(torrent.About??""), "information" },
                { new StringContent(template.Content??""), "description" },
            };
            Log.Verbose("{Site} formdata content: {@MultipartFormDataContent}", site, form);
            var result = await httpClient.PostAsyncWithRetry(postUrl, form);
            var raw = await result.Content.ReadAsStringAsync();
            if (result.StatusCode == HttpStatusCode.Redirect)
            {
                if (raw.Contains("You should be redirected automatically to target URL"))
                {
                    Log.Information("{Site} post success.{NewLine}{Url}", site, Environment.NewLine, result.Headers.Location);
                    return new(200, "Success", true);
                }
                Log.Error("{Site} upload failed. Unknown reson. {NewLine} {Raw}", site, Environment.NewLine, raw);
                return new(500, "Upload failed" + raw, false);
            }
            if (raw.Contains("This torrent already exists"))
            {
                Log.Information("{Site} has already exist.", site);
                return new(200, "Success", true);
            }
            Log.Error("{Site} upload failed.{NewLine}" +
                "Code: {Code}{NewLine}" +
                "{Raw}", site, Environment.NewLine, result.StatusCode, Environment.NewLine, raw);
            return new((int)result.StatusCode, "Failed" + raw, false);
        }

        private bool Valid()
        {
            if (torrent.Data?.TorrentObject is null)
            {
                Log.Fatal("{Site} torrent.Data?.TorrentObject is null", site);
                throw new ArgumentNullException(nameof(torrent.Data.TorrentObject));
            }
            foreach (var tracker in trackers)
            {
                if (!torrent.Data.TorrentObject.Trackers.ToList().Exists(p => p.First().TrimEnd('/').ToLower() == tracker.TrimEnd('/').ToLower()))
                {
                    Log.Error("缺少Tracker：{0}", tracker);
                    return false;
                }
            }
            if (template.Content != null && template.Content.ToLower().EndsWith(".md"))
            {
                Log.Debug("开始寻找{Site} .md文件 {File}", site, template.Content);
                var templateFile = FileHelper.ParseFileFullPath(template.Content, torrent.SettingPath);
                if (File.Exists(templateFile))
                {
                    Log.Debug("找到了{Site} .md文件 {File}", site, template.Content);
                    template.Content = File.ReadAllText(templateFile);
                }
                else
                {
                    Log.Error("发布模板看起来是个.md文件，但是这个.md文件不存在{NewLine}" +
                        "{Source}-->{Dest}", Environment.NewLine, template.Content, templateFile);
                    return false;
                }
            }
            return true;
        }
    }
}
