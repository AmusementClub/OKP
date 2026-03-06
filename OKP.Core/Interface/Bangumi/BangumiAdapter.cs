using OKP.Core.Utils;
using Serilog;
using System.Net;
using System.Net.Http.Json;
using static OKP.Core.Interface.Bangumi.BangumiModels;
using static OKP.Core.Interface.TorrentContent;

namespace OKP.Core.Interface.Bangumi
{
    internal class BangumiAdapter : AdapterBase
    {
        private readonly HttpClient httpClient;
        private readonly Template template;
        private readonly TorrentContent torrent;
        private readonly Uri baseUrl = new("https://bangumi.moe/api/");
        private const string pingUrl = "team/myteam";
        private const string postUrl = "torrent/add";
        private readonly string category;
        private string teamID = "";
        private const string site = "bangumi";

        public BangumiAdapter(TorrentContent torrent, Template template)
        {
            this.torrent = torrent;
            this.template = template;
            var httpClientHandler = new HttpClientHandler()
            {
                CookieContainer = HttpHelper.GlobalCookieContainer,
                AllowAutoRedirect = false
            };
            httpClient = new(httpClientHandler)
            {
                BaseAddress = baseUrl,
            };
            httpClient.DefaultRequestHeaders.Add("user-agent", HttpHelper.GlobalUserAgent);
            if (template.Proxy is not null)
            {
                httpClientHandler.Proxy = new WebProxy(
                    new Uri(template.Proxy),
                    BypassOnLocal: false);
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
            var (result, pingReq) = await PingInternalAsync(httpClient, pingUrl, site);
            if (!result.IsSuccess)
                return result;
            var raw = result.Message;

            var teamList = await pingReq!.Content.ReadFromJsonAsync(BangumiModelsSourceGenerationContext.Default.TeamInfoArray);
            if (teamList == null)
            {
                return result;
            }
            if (teamList.Length == 0)
            {
                Log.Error("{Site} login failed", site);
                return new(403, "Login failed" + raw, false);
            }
            if (template.Name is null)
            {
                Log.Warning("你没有设置{Site}的发布身份，将使用默认身份 {Team}{NewLine}按任意键继续发布", site, teamList.First().name, Environment.NewLine);
                IOHelper.ReadLine();
            }
            else
            {
                foreach (var team in teamList.Where(team => team.name.Equals(template.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    teamID = team._id;
                }
                if (teamID.Equals(""))
                {
                    Log.Error("你设置了{Site}的发布身份为{Team},但是你的账户中没有这个身份。", site, template.Name);
                    return new(500, "Cannot find your team number." + raw, false);
                }
            }
            Log.Debug("{Site} login success", site);
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

            var tags = CastTags(torrent.Tags ?? []);
            var tagIdsString = string.Join(",", tags.Where(t => t != null));

            var form = new MultipartFormDataContent
            {
                { new StringContent(category), "category_tag_id" },
                { new StringContent(template.DisplayName ?? torrent.DisplayName ?? ""), "title" },
                { new StringContent(template.Content ?? ""), "introduction" },
                { new StringContent(tagIdsString), "tag_ids" },
                { new StringContent("undefined"), "btskey" },
                { new StringContent(teamID), "team_id" },
                { torrent.Data.ByteArrayContent, "file", torrent.Data.FileInfo.Name }
            };

            var response = await httpClient.PostAsyncWithRetry(postUrl, form);
            var raw = await response.Content.ReadAsStringAsync();
            var result = await response.Content.ReadFromJsonAsync(BangumiModelsSourceGenerationContext.Default.AddResponse);
            if (!response.IsSuccessStatusCode || result == null)
            {
                Log.Error("{Site} upload failed. Unknown reson. {NewLine} {Raw}", site, Environment.NewLine, raw);
                return new(500, "Upload failed" + raw, false);
            }
            if (!result.success)
            {
                Log.Error("{Site} upload failed. Unknown reson. {NewLine} {Raw}", site, Environment.NewLine, raw);
                return new(500, "Upload failed" + raw, false);
            }
            Log.Information("{Site} post success", site);
            return new(200, "Success", true);
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
        private static List<string?> CastTags(List<ContentTypes>? tags)
        {
            var tagConfig = TagHelper.LoadTagConfig("bangumi.json");
            return tagConfig.FindTagAll(tags);
        }
    }
}
