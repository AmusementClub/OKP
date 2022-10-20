using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OKP.Core.Interface.Dmhy
{
    internal class DmhyAdapter : AdapterBase
    {
        public string? UserAgent { get; set; }
        public string? Pass { get; set; }
        public string? Uid { get; set; }
        public override string? AppToken { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override HttpClient httpClient { get; init; }
        public override List<string> Trackers { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override string Cookie { get => $"pass={Pass};uid={Uid}"; }
        public override string BaseUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override string PingUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override string PostUtl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public DmhyAdapter()
        {
            httpClient = new();
        }

        public override Task<int> PingAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<int> PostAsync(TorrentContent torrent)
        {
            httpClient.BaseAddress = new(BaseUrl);
            MultipartFormDataContent form = new()
            {
                { new StringContent("2"), "sort_id" },
                { new StringContent("2"), "team_id" },
                { new StringContent("2"), "bt_data_title" },
                { new StringContent("2"), "poster_url" },
                { new StringContent("2"), "bt_data_intro" },
                { new StringContent("2"), "tracker" },
                { new StringContent("2"), "MAX_FILE_SIZE" },
                { new StringContent("2"), "bt_file", "[SBSUB&LoliHouse] Detective Conan Hannin No Hanzawa San - 02 [WebRip 1080" },
                { new StringContent("2"), "disable_download_seed_file" },
                { new StringContent("2"), "emule_resource" },
                { new StringContent("2"), "synckey" },
                { new StringContent("2"), "submit" }
            };
            throw new NotImplementedException();
        }

    }
}
