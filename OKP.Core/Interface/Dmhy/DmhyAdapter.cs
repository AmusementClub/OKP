using OKP.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static OKP.Core.Interface.TorrentContent;

namespace OKP.Core.Interface.Dmhy
{
    internal class DmhyAdapter : AdapterBase
    {
        private HttpClient HttpClient { get; init; }
        private Template Template { get; init; }
        private TorrentContent Torrent { get; init; }
        private const string BaseUrl = "https://share.dmhy.org/";
        private const string PingUrl = "topics/add";
        private const string PostUtl = "topics/add";
        private readonly Regex teamReg = new(@"\<select name=""team_id"" id=""team_id""\>[\s\S]*\</select\>", RegexOptions.Multiline);
        private readonly Regex optionReg = new(@"\<option value=""(?<value>\d+)"" label=""(?<name>[^""]+)""");
        private string teamID = "";
        public DmhyAdapter(TorrentContent torrent, Template template)
        {
            HttpClient = new()
            {
                BaseAddress = new(BaseUrl)
            };
            Template = template;
            Torrent = torrent;
            if (template == null)
            {
                return;
            }
            HttpClient.DefaultRequestHeaders.Add("user-agent", template.UserAgent);
            HttpClient.DefaultRequestHeaders.Add("Cookie", template.Cookie);
            HttpClient.BaseAddress = new(BaseUrl);
        }

        public override async Task<HttpResult> PingAsync()
        {
            var pingReq = await HttpClient.GetAsync(PingUrl);
            var raw = await pingReq.Content.ReadAsStringAsync();
            if (!pingReq.IsSuccessStatusCode)
            {
                return new((int)pingReq.StatusCode, raw, false);
            }
            if (raw.Contains(@"<div class=""nav_title text_bold""><img src=""/images/login.gif"" align=""middle"" />&nbsp;登入發佈系統</div>"))
            {
                return new(403, "Login failed" + raw, false);
            }
            var match = teamReg.Match(raw);
            if (match is not null)
            {
                var teams = optionReg.Matches(match.Value);
                if (Template.Name is null)
                {
                    teamID = teams.First().Groups["value"].Value;
                }
                else
                {
                    foreach (var team in teams.ToList())
                    {
                        if (team.Groups["name"].Value == Template.Name)
                        {
                            teamID = team.Groups["value"].Value;
                        }
                    }
                }
            }
            if (teamID == "")
            {
                return new(500, "Cannot find your team number." + raw, false);
            }
            return new(200, "Success", true);
        }

        public override async Task<HttpResult> PostAsync()
        {
            if(Torrent.Data is null)
            {
                throw new NotImplementedException();
            }
            MultipartFormDataContent form = new()
            {
                { new StringContent(Torrent.IsFinished ? "31": "2"), "sort_id" },
                { new StringContent(teamID), "team_id" },
                { new StringContent(Torrent.DisplayName??""), "bt_data_title" },
                { new StringContent(Torrent.Poster??""), "poster_url" },
                { new StringContent(Template.Content??""), "bt_data_intro" },
                { new StringContent(""), "tracker" },
                { new StringContent("2097152"), "MAX_FILE_SIZE" },
                { Torrent.Data.ByteArrayContent, "bt_file", Torrent.Data.FileInfo.Name},
                { new StringContent("0"), "disable_download_seed_file" },
                { new StringContent(""), "emule_resource" },
                { new StringContent(""), "synckey" },
                { new StringContent("提交"), "submit" }
            };
            var result = await HttpClient.PostAsyncWithRetry(PostUtl, form);
            var raw = await result.Content.ReadAsStringAsync();
            if (result.IsSuccessStatusCode)
            {
                if (raw.Contains("<ul><li class=\"text_bold text_blue\">上傳成功</li>"))
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
    }
}
