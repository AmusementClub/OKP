using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OKP.Core.Utils
{
    internal static class HttpHelper
    {
        public static Task<HttpResponseMessage> PostAsyncWithRetry(this HttpClient httpClient,string? uri,HttpContent? content)
        {
            return httpClient.PostAsync(uri, content);
        }
        public static Task<HttpResponseMessage> GetAsyncWithRetry(this HttpClient httpClient, string? uri)
        {
            return httpClient.GetAsync(uri);
        }
    }
}
