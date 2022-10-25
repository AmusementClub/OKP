using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OKP.Core.Interface.TorrentContent;

namespace OKP.Core.Interface.Nyaa
{
    internal class NyaaAdapter : AdapterBase
    {
        private HttpClient HttpClient { get; init; }
        private Template Template { get; init; }
        private TorrentContent Torrent { get; init; }
        public List<string> Trackers { get => throw new NotImplementedException(); }

        public string BaseUrl => "https://nyaa.si/";
        public string PingUrl => "upload";
        public string PostUtl => "upload";
        public NyaaAdapter(TorrentContent torrent, Template template)
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
            if (raw.Contains(@"You are not logged in"))
            {
                return new(403, "Login failed" + raw, false);
            }           
            return new(200, "Success", true);
        }

        public override Task<HttpResult> PostAsync()
        {
            throw new NotImplementedException();
        }
    }
}
