using System.Net;
using System.Net.Http.Json;
using System.Text;
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
        public static async Task<HttpResponseMessage> PostAsyncWithRetry(this HttpClient httpClient, string? uri, HttpContent? content, bool setCookie = true)
        {
            if (httpClient.BaseAddress is null)
            {
                throw new NotImplementedException("httpClient.BaseAddress is null");
            }
            var res = await policy.ExecuteAsync(() => httpClient.PostAsync(uri, content));
            HandleSetCookie(httpClient, setCookie, res);
            return res;
        }
        public static async Task<HttpResponseMessage> GetAsyncWithRetry(this HttpClient httpClient, string? uri, bool setCookie = true)
        {
            if (httpClient.BaseAddress is null)
            {
                throw new NotImplementedException("httpClient.BaseAddress is null");
            }
            var res = await policy.ExecuteAsync(() => httpClient.GetAsync(uri));
            HandleSetCookie(httpClient, setCookie, res);
            return res;
        }
        public static async Task<HttpResponseMessage> PostAsJsonAsyncWithRetry<TValue>(this HttpClient httpClient, string? uri, TValue content, bool setCookie = true, CancellationToken cancellationToken = default)
        {
            if (httpClient.BaseAddress is null)
            {
                throw new NotImplementedException("httpClient.BaseAddress is null");
            }
            var res = await policy.ExecuteAsync(() => httpClient.PostAsJsonAsync(uri, content, cancellationToken));
            HandleSetCookie(httpClient, setCookie, res);
            return res;
        }
        public static void LoadFromTxt(this CookieContainer cookieContainer, string? txtPath)
        {
            if (!File.Exists(txtPath))
            {
                throw new FileNotFoundException(txtPath);
            }
            var jsontext = File.ReadAllLines(txtPath);
            foreach (var line in jsontext)
            {
                var e = line.Split("\t");
                cookieContainer.SetCookies(new(e[0]), e[1]);
            }
        }
        public static void SaveToTxt(this CookieContainer cookieContainer, string txtPath)
        {
            var jsontext = CookieToString(cookieContainer.GetAllCookies());
            File.WriteAllText(txtPath, jsontext);
        }

        private static string CookieToString(CookieCollection cookieCollection)
        {
            StringBuilder stringBuilder = new();
            foreach (var cookie in cookieCollection.ToList())
            {
                stringBuilder.Append($"https://{cookie.Domain.TrimStart('.')}\t");
                stringBuilder.AppendLine($"{cookie.Name}={cookie.Value}; " +
                    $"domain={cookie.Domain}; " +
                    $"path={cookie.Path}; " +
                    $"expires={cookie.Expires.ToString("R")}" +
                    $"{(cookie.Secure ? "; secure" : "")}");
            }
            return stringBuilder.ToString();
        }
        private static void HandleSetCookie(HttpClient httpClient, bool setCookie, HttpResponseMessage res)
        {
            if (setCookie)
            {
                try
                {
                    if (httpClient.BaseAddress is null)
                    {
                        throw new NotImplementedException("httpClient.BaseAddress is null");
                    }
                    foreach (var cookieHeader in res.Headers.GetValues("Set-Cookie"))
                    {
                        GlobalCookieContainer.SetCookies(httpClient.BaseAddress, cookieHeader);
                    }
                }
                catch { }
            }
        }
        public static Task<HttpResponseMessage> PostAsJsonAsyncWithRetry<TValue>(this HttpClient httpClient, string? uri, TValue content, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
        {
            return policy.ExecuteAsync(() => httpClient.PostAsJsonAsync(uri, content, options, cancellationToken));
        }
    }
}
