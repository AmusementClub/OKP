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
        private readonly HttpClient httpClient;
        private readonly Template template;
        private readonly TorrentContent torrent;
        private const string baseUrl = "https://share.dmhy.org/";
        private const string pingUrl = "topics/add";
        private const string postUtl = "topics/add";
        private readonly Regex teamReg = new(@"\<select name=""team_id"" id=""team_id""\>[\s\S]*\</select\>", RegexOptions.Multiline);
        private readonly Regex optionReg = new(@"\<option value=""(?<value>\d+)"" label=""(?<name>[^""]+)""");
        private string teamID = "";
        public DmhyAdapter(TorrentContent torrent, Template template)
        {
            httpClient = new()
            {
                BaseAddress = new(baseUrl)
            };
            this.template = template;
            this.torrent = torrent;
            if (template == null)
            {
                return;
            }
            httpClient.DefaultRequestHeaders.Add("user-agent", template.UserAgent);
            httpClient.DefaultRequestHeaders.Add("Cookie", template.Cookie);
            httpClient.BaseAddress = new(baseUrl);
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
            if (raw.Contains(@"<div class=""nav_title text_bold""><img src=""/images/login.gif"" align=""middle"" />&nbsp;登入發佈系統</div>"))
            {
                return new(403, "Login failed" + raw, false);
            }
            var match = teamReg.Match(raw);
            if (match is not null)
            {
                var teams = optionReg.Matches(match.Value);
                if (template.Name is null)
                {
                    teamID = teams.First().Groups["value"].Value;
                }
                else
                {
                    foreach (var team in teams.ToList())
                    {
                        if (team.Groups["name"].Value == template.Name)
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
            Console.WriteLine("正在发布dmhy");
            if(torrent.Data is null)
            {
                throw new NotImplementedException();
            }
            MultipartFormDataContent form = new()
            {
                { new StringContent(torrent.IsFinished ? "31": "2"), "sort_id" },
                { new StringContent(teamID), "team_id" },
                { new StringContent(torrent.DisplayName??""), "bt_data_title" },
                { new StringContent(torrent.Poster??""), "poster_url" },
                { new StringContent(template.Content??""), "bt_data_intro" },
                { new StringContent(""), "tracker" },
                { new StringContent("2097152"), "MAX_FILE_SIZE" },
                { torrent.Data.ByteArrayContent, "bt_file", torrent.Data.FileInfo.Name},
                { new StringContent("0"), "disable_download_seed_file" },
                { new StringContent(""), "emule_resource" },
                { new StringContent(""), "synckey" },
                { new StringContent("提交"), "submit" }
            };
            var result = await httpClient.PostAsyncWithRetry(postUtl, form);
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
        private bool Valid()
        {
            if (torrent.Data?.TorrentObject is null)
            {
                throw new ArgumentNullException(nameof(torrent.Data.TorrentObject));
            }
            if (template.Content != null && template.Content.ToLower().EndsWith(".html"))
            {
                var templateFile = FileHelper.ParseFileFullPath(template.Content, torrent.Data.FileInfo.FullName);
                if (File.Exists(templateFile))
                {
                    template.Content = File.ReadAllText(templateFile);
                }
                else
                {
                    Console.WriteLine("发布模板看起来是个.html文件，但是这个.html文件不存在");
                    Console.WriteLine("{0}-->{1}", template.Content, templateFile);
                    return false;
                }
            }
            return true;
        }
    }
}
