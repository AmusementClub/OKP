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
        public static CookieContainer GlobalCookieContainer = new();
        private static readonly AsyncRetryPolicy<HttpResponseMessage> policy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        public static async Task<HttpResponseMessage> PostAsyncWithRetry(this HttpClient httpClient, string? uri, HttpContent? content)
        {
            if (httpClient.BaseAddress is null)
            {
                throw new NotImplementedException("httpClient.BaseAddress is null");
            }
            var res = await policy.ExecuteAsync(() => httpClient.PostAsync(uri, content));
            foreach (var cookieHeader in res.Headers.GetValues("Set-Cookie"))
            {
                GlobalCookieContainer.SetCookies(httpClient.BaseAddress, cookieHeader);
            }
            return res;
        }
        public static async Task<HttpResponseMessage> GetAsyncWithRetry(this HttpClient httpClient, string? uri)
        {
            if (httpClient.BaseAddress is null)
            {
                throw new NotImplementedException("httpClient.BaseAddress is null");
            }
            var res = await policy.ExecuteAsync(() => httpClient.GetAsync(uri));
            foreach (var cookieHeader in res.Headers.GetValues("Set-Cookie"))
            {
                GlobalCookieContainer.SetCookies(httpClient.BaseAddress, cookieHeader);
            }
            return res;
        }
        public static async Task<HttpResponseMessage> PostAsJsonAsyncWithRetry<TValue>(this HttpClient httpClient, string? uri, TValue content, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (httpClient.BaseAddress is null)
            {
                throw new NotImplementedException("httpClient.BaseAddress is null");
            }
            var res = await policy.ExecuteAsync(() => httpClient.PostAsJsonAsync(uri, content, options, cancellationToken));
            foreach (var cookieHeader in res.Headers.GetValues("Set-Cookie"))
            {
                GlobalCookieContainer.SetCookies(httpClient.BaseAddress, cookieHeader);
            }
            return res;
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
            var jsontext = JsonSerializer.Serialize(cookieContainer.GetAllCookies(), new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(jsonPath, jsontext);
        }
    }
}
