using OKP.Core.Utils;
using Serilog;
using System.Net;
using static OKP.Core.Interface.TorrentContent;
using System.Net.Http.Json;

namespace OKP.Core.Interface.Acgnx
{
    internal class AcgnxAdapter : AdapterBase
    {
        private readonly HttpClient httpClient;
        private readonly Template template;
        private readonly TorrentContent torrent;
        private readonly string site = "";
        private readonly string baseUrl = "";
        private const string apiUrl = "user.php?o=api&op=upload";

        public AcgnxAdapter(TorrentContent torrent, Template template, string siteType)
        {
            switch (siteType)
            {
                case "asia":
                    site = "acgnx_asia";
                    baseUrl = "https://share.acgnx.se/";
                    break;
                case "global":
                    site = "acgnx_global";
                    baseUrl = "https://www.acgnx.se/";
                    break;
            }

            var httpClientHandler = new HttpClientHandler() { };
            httpClient = new(httpClientHandler)
            {
                BaseAddress = new(baseUrl)
            };
            this.template = template;
            this.torrent = torrent;

            if (template.Cookie == null || template.Cookie.Length == 0)
            {
                Log.Error("Empty {Site} api_token", site);
                IOHelper.ReadLine();
                return;
            }
            else if (template.Cookie.Length != 40)
            {
                Log.Error("Error {Site} api_token length, it must equal to 40", site);
                IOHelper.ReadLine();
                return;
            }

            if (torrent.DisplayName == null || torrent.DisplayName.Length <= 1 || torrent.DisplayName.Length >= 400)
            {
                Log.Error("Error {Site} displayname length, it must between 1 and 400", site);
                IOHelper.ReadLine();
                return;
            }
            if (template.Content != null && template.Content.Length >= 35000)
            {
                Log.Error("Too long {Site} content length, it must be less than 35000", site);
                IOHelper.ReadLine();
                return;
            }
            if (torrent.About != null && torrent.About.Length >= 200)
            {
                Log.Error("Too long {Site} about length, it must be less than 200", site);
                IOHelper.ReadLine();
                return;
            }

            if (template.Proxy is not null)
            {
                httpClientHandler.Proxy = new WebProxy(
                    new Uri(template.Proxy),
                    BypassOnLocal: false);
                httpClientHandler.UseProxy = true;
            }
            httpClient.DefaultRequestHeaders.Add("user-agent", template.UserAgent);
            if (!Valid())
            {
                IOHelper.ReadLine();
                throw new();
            }
        }

        public override async Task<HttpResult> PingAsync()
        {
            MultipartFormDataContent form = new()
            {
                { new StringContent("upload"), "mod" },
                { new StringContent(template.Name??""), "uid" },
                { new StringContent(template.Cookie??""), "api_token" }
            };
            Log.Verbose("{Site} formdata content: {@MultipartFormDataContent}", site, form);
            var result = await httpClient.PostAsyncWithRetry(apiUrl, form);

            if (result.IsSuccessStatusCode)
            {
                var apiContent = await result.Content.ReadFromJsonAsync<AcgnxApiStatus>();
                if (apiContent == null)
                {
                    Log.Error("{Site} api server down", site);
                    return new(403, "Login failed, api server down", false);
                }
                else
                {
                    if (apiContent.Code >= 101 && apiContent.Code < 110)
                    {
                        Log.Error("{Site} login failed", site);
                        return new(403, "Login failed" + apiContent.Value, false);
                    }
                    else
                    {
                        Log.Debug("{Site} login success.", site);
                        return new(200, "Success", true);
                    }
                }
            }
            else
            {
                var raw = await result.Content.ReadAsStringAsync();
                Log.Error("Cannot connect to {Site}.{NewLine}" +
                    "Code: {Code}{NewLine}" +
                    "Raw: {Raw}", site, Environment.NewLine, result.StatusCode, Environment.NewLine, raw);
                return new((int)result.StatusCode, raw, false);
            }
        }

        public override async Task<HttpResult> PostAsync()
        {
            Log.Information("正在发布{Site}", site);
            if (torrent.Data is null)
            {
                Log.Fatal("{Site} torrent.Data is null", site);
                throw new NotImplementedException();
            }
            MultipartFormDataContent form = new()
            {
                { new StringContent("upload"), "mod" },
                { new StringContent(CategoryHelper.SelectCategory(torrent, site)), "sort_id" },
                { torrent.Data.ByteArrayContent, "bt_file", torrent.Data.FileInfo.Name},
                { new StringContent(torrent.DisplayName??""), "title" },
                { new StringContent(template.Content??"Intro None"), "intro" },
                // { new StringContent(""), "emule_resource" },
                // { new StringContent(""), "synckey" },
                { new StringContent(torrent.About??""), "discuss_url" },
                { new StringContent("0"), "Anonymous_Post" },   // 1 = anonymous
                { new StringContent("1"), "Team_Post" },        // 0 = personal post
                { new StringContent(template.Name??""), "uid" },
                { new StringContent(template.Cookie??""), "api_token" }
            };
            Log.Verbose("{Site} formdata content: {@MultipartFormDataContent}", site, form);
            var result = await httpClient.PostAsyncWithRetry(apiUrl, form);
            var apiContent = await result.Content.ReadFromJsonAsync<AcgnxApiStatus>();
            if (result.StatusCode == HttpStatusCode.Redirect && apiContent != null)
            {
                switch (apiContent.Code)
                {
                    case 200:
                        Log.Information($"{site} post {apiContent.Status}.{Environment.NewLine}{baseUrl}/show-{apiContent.Infohash}.html");
                        return new(200, "Success", true);
                    case 302:
                        Log.Information($"{site} post {apiContent.Status}, {apiContent.Value}.{Environment.NewLine}{baseUrl}/show-{apiContent.Infohash}.html");
                        return new(200, "Success", true);
                    default:
                        Log.Error($"{site} post {apiContent.Status}, {apiContent.Value}.");
                        return new(apiContent.Code, $"Failed, {apiContent.Value}.", false);
                }
            }
            else
            {
                var raw = await result.Content.ReadAsStringAsync();
                return new((int)result.StatusCode, "Failed" + raw, false);
            }
        }

        private bool Valid()
        {
            if (torrent.Data?.TorrentObject is null)
            {
                Log.Fatal("{Site} torrent.Data?.TorrentObject is null", site);
                throw new ArgumentNullException(nameof(torrent.Data.TorrentObject));
            }

            if (template.Content != null && template.Content.ToLower().EndsWith(".html"))
            {
                Log.Debug("开始寻找{Site} .html文件 {File}", site, template.Content);
                var templateFile = FileHelper.ParseFileFullPath(template.Content, torrent.SettingPath);
                if (File.Exists(templateFile))
                {
                    Log.Debug("找到了{Site} .html文件 {File}", site, template.Content);
                    template.Content = File.ReadAllText(templateFile);
                }
                else
                {
                    Log.Error("发布模板看起来是个.html文件，但是这个.html文件不存在{NewLine}" +
                        "{Source}-->{Dest}", Environment.NewLine, template.Content, templateFile);
                    return false;
                }
            }
            return true;
        }
    }

    internal class AcgnxApiStatus
    {
        internal string? Status { get; set; }
        internal ushort Code { get; set; }
        internal string? Value { get; set; }
        internal string? Infohash { get; set; }
        internal string? Title { get; set; }
    }
}