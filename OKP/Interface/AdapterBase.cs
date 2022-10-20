using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OKP.Core.Interface
{
    internal abstract class AdapterBase
    {
        abstract public string? AppToken { get; set; }
        abstract public HttpClient httpClient { get; init; }
        abstract public Task<int> PingAsync();
        abstract public Task<int> PostAsync(TorrentContent torrent);
        abstract public List<string> Trackers { get; set; }
        abstract public string Cookie { get; }
        abstract public string BaseUrl { get; set; }
        abstract public string PingUrl { get; set; }
        abstract public string PostUtl { get; set; }
    }

    internal interface IAdapter
    {
        

    }
}
