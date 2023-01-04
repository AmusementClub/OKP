using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OKP.Core.Interface;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;

namespace OKP.Core.Utils
{
    internal static class HttpHelper
    {
        public static CookieContainer GlobalCookieContainer = new ();
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
        public static void LoadFromJson(this CookieContainer cookieContainer, string? jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException(jsonPath);
            }
            var cookieCollection = JsonSerializer.Deserialize<CookieCollection>(File.ReadAllText(jsonPath));
            if (cookieCollection != null)
            {
                cookieContainer.Add(cookieCollection);
            }
        }
        public static void SaveToJson(this CookieContainer cookieContainer, string jsonPath)
        {
            var jsontext = JsonSerializer.Serialize(cookieContainer.GetAllCookies());
            File.WriteAllText(jsonPath, jsontext);
        }
        public static Task<HttpResponseMessage> PostAsJsonAsyncWithRetry<TValue>(this HttpClient httpClient, string? uri, TValue content, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
        {
            return policy.ExecuteAsync(() => httpClient.PostAsJsonAsync(uri, content, options, cancellationToken));
        }
    }
}
