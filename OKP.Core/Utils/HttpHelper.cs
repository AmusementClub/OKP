using Polly;
using Polly.Retry;
using Serilog;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace OKP.Core.Utils
{
    internal static partial class HttpHelper
    {
        public static CookieContainer GlobalCookieContainer = new();
        public static string GlobalUserAgent = "";
        public static Regex UaRegex = UserAgentPattern();
        private static readonly ResiliencePipeline<HttpResponseMessage> pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 5,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args =>
                {
                    // Retry on transient HTTP errors (5xx) and HttpRequestException
                    if (args.Outcome.Exception is HttpRequestException)
                        return ValueTask.FromResult(true);
                    var result = args.Outcome.Result;
                    var should = result != null && (int)result.StatusCode >= 500;
                    return ValueTask.FromResult(should);
                }
            })
            .Build();
        public static async Task<HttpResponseMessage> PostAsyncWithRetry(this HttpClient httpClient, string? uri, HttpContent? content, bool setCookie = true)
        {
            if (httpClient.BaseAddress is null)
            {
                throw new NotImplementedException("httpClient.BaseAddress is null");
            }
            var res = await pipeline.ExecuteAsync(async _ => await httpClient.PostAsync(uri, content), ResilienceContextPool.Shared.Get("PostAsync"));
            HandleSetCookie(httpClient, setCookie, res);
            return res;
        }
        public static async Task<HttpResponseMessage> GetAsyncWithRetry(this HttpClient httpClient, string? uri, bool setCookie = true)
        {
            if (httpClient.BaseAddress is null)
            {
                throw new NotImplementedException("httpClient.BaseAddress is null");
            }
            var res = await pipeline.ExecuteAsync(async _ => await httpClient.GetAsync(uri), ResilienceContextPool.Shared.Get("GetAsync"));
            HandleSetCookie(httpClient, setCookie, res);
            return res;
        }
        public static async Task<HttpResponseMessage> PostAsJsonAsyncWithRetry<TValue>(this HttpClient httpClient, string? uri, TValue content, JsonTypeInfo<TValue> jsonTypeInfo, bool setCookie = true, CancellationToken cancellationToken = default)
        {
            if (httpClient.BaseAddress is null)
            {
                throw new NotImplementedException("httpClient.BaseAddress is null");
            }
            var res = await pipeline.ExecuteAsync(async _ => await httpClient.PostAsJsonAsync(uri, content, jsonTypeInfo, cancellationToken), ResilienceContextPool.Shared.Get("PostAsJsonAsync", cancellationToken));
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

        [GeneratedRegex(@"\((?<info>.*?)\)(\s|$)|(?<name>.*?)\/(?<version>.*?)(\s|$)")]
        private static partial Regex UserAgentPattern();
    }
}
