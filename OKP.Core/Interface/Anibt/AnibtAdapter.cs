using OKP.Core.Utils;
using Serilog;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using static OKP.Core.Interface.TorrentContent;

namespace OKP.Core.Interface.Anibt
{
    internal class AnibtAdapter : AdapterBase
    {
        private readonly HttpClient httpClient;
        private readonly Template template;
        private readonly TorrentContent torrent;
        private readonly Uri baseUrl = new("https://anibt.net/api/");
        private const string pingUrl = "subtitle-groups/me";
        private const string postUrl = "releases/publish";
        private const string site = "anibt";

        public AnibtAdapter(TorrentContent torrent, Template template)
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
            if (!string.IsNullOrWhiteSpace(HttpHelper.GlobalUserAgent))
            {
                httpClient.DefaultRequestHeaders.Add("user-agent", HttpHelper.GlobalUserAgent);
            }
            this.template = template;
            this.torrent = torrent;

            if (string.IsNullOrWhiteSpace(template.ApiToken))
            {
                Log.Error("你没有配置{Site}的api_token，无法发布", site);
                IOHelper.ReadLine();
                throw new();
            }
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", template.ApiToken);

            if (string.IsNullOrWhiteSpace(torrent.AnimeIdType) || string.IsNullOrWhiteSpace(torrent.AnimeId))
            {
                Log.Error("{Site}需要在setting里配置anime_id_type和anime_id", site);
                IOHelper.ReadLine();
                throw new();
            }

            if (template.Proxy is not null)
            {
                httpClientHandler.Proxy = new WebProxy(
                    new Uri(template.Proxy),
                    BypassOnLocal: false);
            }

            if (!Valid())
            {
                IOHelper.ReadLine();
                throw new();
            }
        }

        public override async Task<HttpResult> PingAsync()
        {
            var (result, _) = await PingInternalAsync(httpClient, pingUrl, site);
            if (!result.IsSuccess) return result;
            var raw = result.Message;

            var me = TryParse(raw);
            if (me is null || !me.Ok)
            {
                Log.Error("{Site} login failed", site);
                return new(403, "Login failed" + raw, false);
            }
            Log.Debug("{Site} login success, {Name}", site, me.Data?.Name);
            return new(200, "Success", true);
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
                { torrent.Data.ByteArrayContent, "torrent", torrent.Data.FileInfo.Name },
                { new StringContent(template.DisplayName ?? torrent.DisplayName ?? ""), "title" },
                { new StringContent(torrent.AnimeIdType ?? ""), "animeIdType" },
                { new StringContent(torrent.AnimeId ?? ""), "animeId" },
                { new StringContent(template.Content ?? ""), "notes" }
            };
            Log.Verbose("{Site} formdata content: {@MultipartFormDataContent}", site, form);
            var result = await httpClient.PostAsyncWithRetry(postUrl, form, setCookie: false);
            var raw = await result.Content.ReadAsStringAsync();
            var resp = TryParse(raw);

            if (result.IsSuccessStatusCode && (resp is null || resp.Ok))
            {
                Log.Information("{Site} post success", site);
                return new(200, "Success", true);
            }
            Log.Error("{Site} upload failed.{NewLine}" +
                "Code: {Code}{NewLine}" +
                "{Raw}", site, Environment.NewLine, result.StatusCode, Environment.NewLine, raw);
            var message = resp?.Message ?? resp?.Error;
            return new((int)result.StatusCode, "Failed" + (message ?? raw), false);
        }

        private bool Valid()
        {
            if (torrent.Data?.TorrentObject is null)
            {
                Log.Fatal("{Site} torrent.Data?.TorrentObject is null", site);
                throw new ArgumentNullException(nameof(torrent.Data.TorrentObject));
            }
            return ValidTemplate(template, site, torrent.SettingPath);
        }

        private static AnibtResponse? TryParse(string raw)
        {
            try
            {
                return JsonSerializer.Deserialize(raw, AnibtModelsSourceGenerationContext.Default.AnibtResponse);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    internal class AnibtResponse
    {
        public bool Ok { get; set; }
        public AnibtData? Data { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
    }

    internal class AnibtData
    {
        public string? Name { get; set; }
    }

    [JsonSerializable(typeof(AnibtResponse))]
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString)
    ]
    internal partial class AnibtModelsSourceGenerationContext : JsonSerializerContext;
}
