using OKP.Core.Utils;
using Serilog;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using static OKP.Core.Interface.TorrentContent;

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
            var httpClientHandler = new HttpClientHandler()
            {
                CookieContainer = HttpHelper.GlobalCookieContainer,
                AllowAutoRedirect = false
            };
            httpClient = new(httpClientHandler)
            {
                BaseAddress = new(baseUrl)
            };
            httpClient.DefaultRequestHeaders.Add("user-agent", HttpHelper.GlobalUserAgent);
            this.template = template;
            this.torrent = torrent;
            var cookieToken = HttpHelper.GlobalCookieContainer.GetCookies(httpClient.BaseAddress).ToList().Find(p => p.Name.ToLower() == "token");
            if (cookieToken is null)
            {
                Log.Error("Cannot find token of {Site}", site);
                IOHelper.ReadLine();
                return;
            }
            template.Cookie = cookieToken.Value;
            if (torrent.DisplayName is not {Length: > 1 and < 400})
            {
                Log.Error("Error {Site} displayname length, it must between 1 and 400", site);
                IOHelper.ReadLine();
                return;
            }
            if (template.Content is {Length: >= 35000})
            {
                Log.Error("Too long {Site} content length, it must be less than 35000", site);
                IOHelper.ReadLine();
                return;
            }
            if (torrent.About is {Length: >= 200})
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

            if (!Valid())
            {
                IOHelper.ReadLine();
                throw new();
            }
        }

        public override async Task<HttpResult> PingAsync()
        {
            HttpRequestMessage request = new(HttpMethod.Post, apiUrl);
            request.Content = new MultipartFormDataContent()
            {
                { new StringContent("upload"), "mod" },
                { new StringContent(template.Name??""), "uid" },
                { new StringContent(template.Cookie??""), "api_token" }
            };
            Log.Verbose("{Site} formdata content: {@MultipartFormDataContent}", site, request.Content);
            var result = await httpClient.SendAsync(request);
            var raw = await result.Content.ReadAsStringAsync();

            if (result.IsSuccessStatusCode && !raw.Contains("<html"))
            {
                var apiContent = await result.Content.ReadFromJsonAsync<AcgnxApiStatus>(new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                });
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
                        Log.Debug("{Site} login success", site);
                        return new(200, "Success", true);
                    }
                }
            }
            else
            {
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
                { new StringContent(CategoryHelper.SelectCategory(torrent.Tags, site)), "sort_id" },
                { torrent.Data.ByteArrayContent, "bt_file", torrent.Data.FileInfo.Name},
                { new StringContent(template.DisplayName ?? torrent.DisplayName ?? ""), "title" },
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
            var raw = await result.Content.ReadAsStringAsync();
            var apiContent = await result.Content.ReadFromJsonAsync<AcgnxApiStatus>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            });

            if (result.StatusCode == HttpStatusCode.OK && apiContent != null && !raw.Contains("<html"))
            {
                switch (apiContent.Code)
                {
                    case 200:
                        Log.Information("{Site} post {Status}.{NewLine}{Url}", site, apiContent.Status, Environment.NewLine, $"{baseUrl}show-{apiContent.Infohash}.html");
                        return new(200, "Success", true);
                    case 302:
                        Log.Information("{Site} post {Status}, {ApiValue}.{NewLine}{Url}", site, apiContent.Status, apiContent.Value, Environment.NewLine, $"{baseUrl}show-{apiContent.Infohash}.html");
                        return new(200, "Success", true);
                    default:
                        Log.Error("{Site} post {Status}, {ApiValue}", site, apiContent.Status, apiContent.Value);
                        return new(apiContent.Code, $"Failed, {apiContent.Value}.", false);
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
        public string? Status { get; set; }
        public ushort Code { get; set; }
        public string? Value { get; set; }
        public string? Infohash { get; set; }
        public string? Title { get; set; }
    }
}
