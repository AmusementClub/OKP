using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OKP.Core.Interface.Nyaa
{
    internal class NyaaAdapter : AdapterBase
    {
        public HttpClient httpClient { get => throw new NotImplementedException(); init => throw new NotImplementedException(); }
        public List<string> Trackers { get => throw new NotImplementedException(); }

        public string BaseUrl => "https://nyaa.si/";
        public string PingUrl { get => throw new NotImplementedException(); }
        public string PostUtl { get => throw new NotImplementedException(); }
        public string Cookie { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override Task<int> PingAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<int> PostAsync()
        {
            throw new NotImplementedException();
        }
    }
}
