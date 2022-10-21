using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using OKP.Core.Interface;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;

namespace OKP.Core.Utils
{
    internal static class HttpHelper
    {
        private static readonly AsyncRetryPolicy<HttpResponseMessage> policy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        public static Task<HttpResponseMessage> PostAsyncWithRetry(this HttpClient httpClient, string? uri, HttpContent? content)
        {
            return policy.ExecuteAsync(() => httpClient.PostAsync(uri, content));
        }
        public static Task<HttpResponseMessage> GetAsyncWithRetry(this HttpClient httpClient, string? uri)
        {
            return policy.ExecuteAsync(() => httpClient.GetAsync(uri));
        }
    }
}
