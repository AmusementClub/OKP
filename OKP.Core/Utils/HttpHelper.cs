using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using Serilog;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace OKP.Core.Utils
{
    internal static class HttpHelper
    {
        public static CookieContainer GlobalCookieContainer = new();
        public static string GlobalUserAgent = "";
        public static Regex UaRegex = new(@"\((?<info>.*?)\)(\s|$)|(?<name>.*?)\/(?<version>.*?)(\s|$)");
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
            GlobalUserAgent = jsontext[0].Split('\t')[1];
            if (!UaRegex.IsMatch(GlobalUserAgent))
            {
                Log.Fatal("不合法的UA{UA}", GlobalUserAgent);
                throw new Exception("invalid GlobalUserAgent");
            }
            foreach (var line in jsontext.Skip(1))
            {
                var e = line.Split("\t");
                cookieContainer.SetCookies(new(e[0]), e[1]);
            }
        }
        public static void SaveToTxt(this CookieContainer cookieContainer, string txtPath, string userAgent)
        {
            var text = CookieToString(cookieContainer.GetAllCookies(), userAgent);
            File.WriteAllText(txtPath, text);
        }

        private static string CookieToString(CookieCollection cookieCollection, string userAgent)
        {
            StringBuilder sb = new();
            sb.AppendLine($"user-agent:\t{userAgent}");
            foreach (var cookie in cookieCollection.ToList())
            {
                sb.Append($"https://{cookie.Domain.TrimStart('.')}\t");
                sb.AppendLine($"{cookie.Name}={cookie.Value}; " +
                    $"domain={cookie.Domain}; " +
                    $"path={cookie.Path}; " +
                    $"expires={cookie.Expires.ToString("R")}" +
                    $"{(cookie.Secure ? "; secure" : "")}");
            }
            return sb.ToString();
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
    }
}
