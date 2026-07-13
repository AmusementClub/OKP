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
        private const string otherPostUrl = "other-releases/publish";
        private const string site = "anibt";
        // 非番剧分类：manga/music/raw/stage，走 other-releases 端点、不需要 bgmid；null 表示番剧
        private readonly string? otherCategory;

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

            // 按 tag 分流：漫画/音乐/RAW/舞台剧走 other-releases（不需要 bgmid），其余当番剧
            otherCategory = ResolveCategory();

            // 番剧才需要 bgmid。站点侧不做匹配，没填就 OKP 自己查：有把握直接填，没把握列候选让人选
            if (otherCategory is null && string.IsNullOrWhiteSpace(torrent.AnimeId))
            {
                ResolveAnimeId();
            }
        }

        // 带 Anime tag 一律当番剧；否则按 anibt.json 映射到 manga/music/raw/stage，映射不到也当番剧
        private string? ResolveCategory()
        {
            if (torrent.Tags?.Contains(ContentTypes.Anime) == true)
            {
                return null;
            }
            return TagHelper.LoadTagConfig("anibt.json").FindTag(torrent.Tags);
        }

        private void ResolveAnimeId()
        {
            var title = template.DisplayName ?? torrent.DisplayName;
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }
            Log.Information("{Site} 没填 bgmid，尝试自动匹配：{Title}", site, title);
            BangumiMatcher.MatchResult match;
            try
            {
                match = BangumiMatcher.MatchAsync(title, template.Proxy).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warning("{Site} 自动匹配 bgmid 出错，跳过：{Msg}", site, ex.Message);
                return;
            }

            if (match.Status == BangumiMatcher.MatchStatus.Matched)
            {
                torrent.AnimeIdType = AnimeIdTypes.Bgm;
                torrent.AnimeId = match.BgmId.ToString();
                Log.Information("{Site} 自动匹配到 bgmid={BgmId}《{Name}》(相似度 {Score:0.00})，不对的话请在确认发布前中止",
                    site, match.BgmId, match.NameCn ?? match.Name, match.Score);
                return;
            }

            if (match.Candidates.Count == 0)
            {
                Log.Warning("{Site} 没搜到匹配的 bgmid，将不带 bgmid 发布（放送表里会显示未匹配）", site);
                return;
            }
            Log.Warning("{Site} 拿不准 bgmid，你自己挑一个（标题：{Title}）", site, title);
            for (var i = 0; i < match.Candidates.Count; i++)
            {
                var c = match.Candidates[i];
                Log.Information("  [{Index}] bgmid={BgmId} {Name} / {NameCn} {Date} (相似度 {Score:0.00})",
                    i + 1, c.BgmId, c.Name, c.NameCn, c.Date, c.Score);
            }
            Log.Information("输入编号选择，直接回车跳过（不带 bgmid 发布）：");
            var input = IOHelper.ReadLine();
            if (int.TryParse(input, out var pick) && pick >= 1 && pick <= match.Candidates.Count)
            {
                var chosen = match.Candidates[pick - 1];
                torrent.AnimeIdType = AnimeIdTypes.Bgm;
                torrent.AnimeId = chosen.BgmId.ToString();
                Log.Information("{Site} 已选 bgmid={BgmId}《{Name}》", site, chosen.BgmId, chosen.NameCn ?? chosen.Name);
            }
            else
            {
                Log.Information("{Site} 跳过 bgmid，将不带 bgmid 发布", site);
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
            // 漫画/音乐/RAW/舞台剧走 other-releases：只要 category + title + 种子，不绑 bgmid
            if (otherCategory is not null)
            {
                MultipartFormDataContent otherForm = new()
                {
                    { torrent.Data.ByteArrayContent, "torrent", torrent.Data.FileInfo.Name },
                    { new StringContent(otherCategory), "category" },
                    { new StringContent(template.DisplayName ?? torrent.DisplayName ?? ""), "title" }
                };
                Log.Verbose("{Site} other-releases formdata content: {@MultipartFormDataContent}", site, otherForm);
                return await PostFormAsync(otherPostUrl, otherForm);
            }

            MultipartFormDataContent form = new()
            {
                { torrent.Data.ByteArrayContent, "torrent", torrent.Data.FileInfo.Name },
                { new StringContent(template.DisplayName ?? torrent.DisplayName ?? ""), "title" },
                { new StringContent(template.Content ?? ""), "notes" }
            };
            // 没填 anime_id 就交给站点自己匹配或人工添加，不强求发布者填
            if (torrent.AnimeIdType is not null && !string.IsNullOrWhiteSpace(torrent.AnimeId))
            {
                form.Add(new StringContent(torrent.AnimeIdType.Value.ToString().ToLowerInvariant()), "animeIdType");
                form.Add(new StringContent(torrent.AnimeId), "animeId");
            }
            Log.Verbose("{Site} formdata content: {@MultipartFormDataContent}", site, form);
            return await PostFormAsync(postUrl, form);
        }

        private async Task<HttpResult> PostFormAsync(string url, MultipartFormDataContent form)
        {
            var result = await httpClient.PostAsyncWithRetry(url, form, setCookie: false);
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
