using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OKP.Core.Interface.Bangumi
{
    internal class BangumiAdapter : AdapterBase
    {
        public string? AppToken { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public HttpClient httpClient { get => throw new NotImplementedException(); init => throw new NotImplementedException(); }
        public List<string> Trackers { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public string Cookie => throw new NotImplementedException();

        public string BaseUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string PingUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string PostUtl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override Task<HttpResult> PingAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<HttpResult> PostAsync()
        {

            throw new NotImplementedException();
        }
        private async Task<string> UploadTorrent(TorrentContent torrent)
        {
            if (torrent.Data is null)
            {
                throw new NotImplementedException();
            }
            var form = new MultipartFormDataContent
            {
                { torrent.Data.ByteArrayContent, "file", torrent.Data.FileInfo.Name },
                {new StringContent("xx"),"team_i" }
            };
            var response = await httpClient.PostAsync("/api/v2/torrent/upload", form);
            var resstr = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<UploadResponse>(resstr);
            if(result == null || !result.success || result.file_id == null)
            {
                throw new HttpRequestException("Failed to upload torrent file");
            }
            return result.file_id;
        }

        public class UploadResponse
        {
            public bool success { get; set; }
            public string? file_id { get; set; }
            public string[][]? content { get; set; }
            public object[]? torrents { get; set; }
        }

    }
}
