using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OKP.Core.Interface.Nyaa
{
    internal class NyaaAdapter : AdapterBase
    {
        public override string? AppToken { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override HttpClient httpClient { get => throw new NotImplementedException(); init => throw new NotImplementedException(); }
        public override List<string> Trackers { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override string Cookie => throw new NotImplementedException();

        public override string BaseUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override string PingUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override string PostUtl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override Task<int> PingAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<int> PostAsync(TorrentContent name)
        {
            throw new NotImplementedException();
        }
    }
}
