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
        private const string uploadUrl = "v2/torrent/upload";
        private string category;
        private string teamID = "";
        private string tagID = "";  // string[]?
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
            var pingReq = await httpClient.GetAsync(pingUrl);
            var raw = await pingReq.Content.ReadAsStringAsync();
            var teamList = await pingReq.Content.ReadFromJsonAsync(BangumiModelsSourceGenerationContext.Default.TeamInfoArray);
            if (!pingReq.IsSuccessStatusCode || teamList == null)
            {
                Log.Error("Cannot connect to {Site}.{NewLine}" +
                   "Code: {Code}{NewLine}" +
                   "Raw: {Raw}", site, Environment.NewLine, pingReq.StatusCode, Environment.NewLine, raw);
                return new((int)pingReq.StatusCode, raw, false);
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
                foreach (var team in teamList.Where(team => team.name.ToLower() == template.Name.ToLower()))
                {
                    teamID = team._id;
                    tagID = team.tag_id;
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
            var fileId = await UploadTorrent(torrent);
            Log.Information("正在发布{Site}", site);
            if (torrent.Data is null)
            {
                Log.Fatal("{Site} torrent.Data is null", site);
                throw new NotImplementedException();
            }
            AddRequest addRequest = new()
            {
                category_tag_id = category,
                title = template.DisplayName ?? torrent.DisplayName ?? "",
                introduction = template.Content ?? "",
                tag_ids = CastTags(torrent.Tags ?? new List<ContentTypes>()).ToArray(),
                team_id = teamID,
                teamsync = false,
                file_id = fileId,
            };
            var response = await httpClient.PostAsJsonAsyncWithRetry(postUrl, addRequest, BangumiModelsSourceGenerationContext.Default.AddRequest);
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
        private async Task<string> UploadTorrent(TorrentContent torrent)
        {
            if (torrent.Data is null)
            {
                throw new NotImplementedException();
            }
            var form = new MultipartFormDataContent
            {
                { torrent.Data.ByteArrayContent, "file", torrent.Data.FileInfo.Name }
            };
            var response = await httpClient.PostAsyncWithRetry(uploadUrl, form);
            var result = await response.Content.ReadFromJsonAsync(BangumiModelsSourceGenerationContext.Default.UploadResponse);
            if (result == null || !result.success || result.file_id == null)
            {
                Log.Debug("可能是 {Site} 发布频率限制，先等 1 分钟", site);
                Thread.Sleep(60000);
                response = await httpClient.PostAsyncWithRetry(uploadUrl, form);
                result = await response.Content.ReadFromJsonAsync(BangumiModelsSourceGenerationContext.Default.UploadResponse);
                if (result == null || !result.success || result.file_id == null)
                {
                    throw new HttpRequestException("Failed to upload torrent file");
                }
            }
            return result.file_id;
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
                Log.Debug("开始寻找{Site} html文件 {File}", site, template.Content);
                var templateFile = FileHelper.ParseFileFullPath(template.Content, torrent.SettingPath);
                if (File.Exists(templateFile))
                {
                    Log.Debug("找到了{Site} html文件 {File}", site, templateFile);
                    template.Content = File.ReadAllText(templateFile);
                }
                else
                {
                    Log.Error("发布模板看起来是个.html文件，但是这个.html文件不存在{NewLine}" +
                        "{Source}-->{Dest}", template.Content, Environment.NewLine, templateFile);
                    return false;
                }
            }
            return true;
        }
        private static List<string?> CastTags(List<ContentTypes>? tags)
        {
            var tagConfig = TagHelper.LoadTagConfig("bangumi.json");
            return tagConfig.FindTagAll(tags);
        }
    }
}
