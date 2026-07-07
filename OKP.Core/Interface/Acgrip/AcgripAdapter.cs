using OKP.Core.Utils;
using Serilog;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static OKP.Core.Interface.TorrentContent;

namespace OKP.Core.Interface.Acgrip
{
    internal class AcgripAdapter : AdapterBase
    {
        private readonly HttpClient httpClient;
        private readonly Template template;
        private readonly TorrentContent torrent;
        private readonly Uri baseUrl = new("https://acg.rip/");
        private const string pingUrl = "cp/posts/upload";
        private const string postUtl = "cp/posts";
        private const string apiPostUrl = "api/post";
        private string category;
        private readonly Regex personalReg = new(@"class=""panel-title""\>(.*?)\</div\>");
        private readonly Regex teamReg = new(@"class=""panel-title-right""\>(.*?)\</div\>");
        private readonly Regex tokenReg = new(@"\<meta\sname=""csrf-token""\scontent=""(.*)""\s/\>");
        private readonly List<string> trackers = new() { "http://t.acg.rip:6699/announce" };
        private string authenticityToken = "";
        private const string site = "acgrip";
        private bool HasApiToken => !string.IsNullOrWhiteSpace(template.ApiToken);

        public AcgripAdapter(TorrentContent torrent, Template template)
        {
            var httpClientHandler = new HttpClientHandler()
            {
                CookieContainer = HttpHelper.GlobalCookieContainer,
                AllowAutoRedirect = false
            };
            httpClient = new(httpClientHandler)
            {
                BaseAddress = baseUrl,
            };
            if (!string.IsNullOrWhiteSpace(HttpHelper.GlobalUserAgent))
            {
                httpClient.DefaultRequestHeaders.Add("user-agent", HttpHelper.GlobalUserAgent);
            }
            this.template = template;
            this.torrent = torrent;
            category = CategoryHelper.SelectCategory(torrent.Tags, site);

            if (HasApiToken)
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-API-TOKEN", template.ApiToken);
            }

            if (template.Proxy is not null)
            {
                httpClientHandler.Proxy = new WebProxy(
                    new Uri(template.Proxy),
                    BypassOnLocal: false);
            }

            if (!Valid())
            {
                IOHelper.ReadLine();
                throw new();
            }
        }

        public override async Task<HttpResult> PingAsync()
        {
            if (HasApiToken)
            {
                return await PingApiTokenAsync();
            }

            var (result, _) = await PingInternalAsync(httpClient, pingUrl, site);
            if (!result.IsSuccess) return result;
            var raw = result.Message;

            if (raw.Contains(@"继续操作前请注册或者登录"))
            {
                Log.Error("{Site} login failed", site);
                return new(403, "Login failed" + raw, false);
            }

            // Some accounts don’t belong to team
            var match = teamReg.Match(raw);
            if (match == Match.Empty)
            {
                match = personalReg.Match(raw);
            }

            if (match == Match.Empty || match.Groups[1].Value != template.Name)
            {
                Log.Error("你设置了{Site}的发布身份为{Team},但是你的Cookie对应的账户是{Name}。", site, template.Name, match?.Groups[1].Value ?? "undefined");
                return new(500, "Cannot find your team number." + raw, false);
            }
            var tokenMatch = tokenReg.Match(raw);
            if (!tokenMatch.Success)
            {
                Log.Error("登录失败，找不到authenticityToken");
                return new(500, "Cannot find authenticityToken." + raw, false);
            }
            authenticityToken = tokenMatch.Groups[1].Value;
            Log.Debug("{Site} login success", site);
            return new(200, "Success", true);
        }

        private async Task<HttpResult> PingApiTokenAsync()
        {
            var result = await httpClient.PostAsyncWithRetry(apiPostUrl, null, setCookie: false);
            if (result.IsSuccessStatusCode)
            {
                Log.Debug("{Site} api token validation success", site);
                return new(200, "Success", true);
            }

            var apiResponse = await ReadApiResponseAsync(result);
            var message = apiResponse.ApiResult.HasError
                ? apiResponse.ApiResult.GetErrorMessage()
                : apiResponse.GetRawText();
            Log.Error("{Site} api token validation failed. {Message}", site, message);
            return new((int)result.StatusCode, "Login failed, " + message, false);
        }

        public override async Task<HttpResult> PostAsync()
        {
            Log.Information("正在发布{Site}", site);
            if (torrent.Data is null)
            {
                Log.Fatal("{Site} torrent.Data is null", site);
                throw new NotImplementedException();
            }
            var apiTokenMode = HasApiToken;
            using var form = BuildPostForm(apiTokenMode);
            Log.Verbose("{Site} formdata content: {@MultipartFormDataContent}", site, form);
            var result = await httpClient.PostAsyncWithRetry(apiTokenMode ? apiPostUrl : postUtl, form, setCookie: !apiTokenMode);
            if (apiTokenMode)
            {
                return await ParseApiPostResultAsync(result);
            }

            var raw = await result.Content.ReadAsStringAsync();
            return ParseBrowserPostResult(result, raw);
        }

        private MultipartFormDataContent BuildPostForm(bool apiTokenMode)
        {
            if (torrent.Data is null)
            {
                Log.Fatal("{Site} torrent.Data is null", site);
                throw new NotImplementedException();
            }

            MultipartFormDataContent form = new();
            if (!apiTokenMode)
            {
                form.Add(new StringContent(authenticityToken), "authenticity_token");
            }
            form.Add(new StringContent(category), "post[category_id]");
            if (apiTokenMode)
            {
                form.Add(new StringContent("0"), "post[series_id]");
            }
            else
            {
                form.Add(new StringContent("1"), "post[post_as_team]");
            }
            form.Add(torrent.Data.ByteArrayContent, "post[torrent]", torrent.Data.FileInfo.Name);
            form.Add(new StringContent(template.DisplayName ?? torrent.DisplayName ?? ""), "post[title]");
            form.Add(new StringContent(template.Content ?? ""), "post[content]");
            if (!apiTokenMode)
            {
                form.Add(new StringContent("发布"), "commit");
            }

            return form;
        }

        private static async Task<HttpResult> ParseApiPostResultAsync(HttpResponseMessage result)
        {
            var apiResponse = await ReadApiResponseAsync(result);
            var apiResult = apiResponse.ApiResult;

            if (apiResult.HasError)
            {
                var message = apiResult.GetErrorMessage();
                Log.Error("{Site} api upload failed. {Message}", site, message);
                return new((int)result.StatusCode, "Failed, " + message, false);
            }

            if (result.IsSuccessStatusCode)
            {
                var url = string.IsNullOrWhiteSpace(apiResult.Id) ? null : $"https://acg.rip/t/{apiResult.Id}";
                if (url is not null)
                {
                    Log.Information("{Site} post success.{NewLine}{Url}", site, Environment.NewLine, url);
                }
                else
                {
                    Log.Information("{Site} post success", site);
                }
                return new(200, "Success", true);
            }

            var rawText = apiResponse.GetRawText();
            Log.Error("{Site} upload failed.{NewLine}" +
                "Code: {Code}{NewLine}" +
                "{Raw}", site, Environment.NewLine, result.StatusCode, Environment.NewLine, rawText);
            return new((int)result.StatusCode, "Failed" + rawText, false);
        }

        private static async Task<AcgripApiResponse> ReadApiResponseAsync(HttpResponseMessage result)
        {
            var raw = await result.Content.ReadAsByteArrayAsync();
            return new(raw, ParseApiResponse(raw));
        }

        private static AcgripApiResult ParseApiResponse(ReadOnlySpan<byte> raw)
        {
            var reader = new Utf8JsonReader(raw, isFinalBlock: true, state: default);
            string? error = null;
            string? message = null;
            string? id = null;

            try
            {
                while (reader.Read())
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        continue;
                    }

                    if (reader.ValueTextEquals("error"u8))
                    {
                        error = ReadNextStringValue(ref reader);
                    }
                    else if (reader.ValueTextEquals("message"u8))
                    {
                        message = ReadNextStringValue(ref reader);
                    }
                    else if (reader.ValueTextEquals("id"u8))
                    {
                        id = ReadNextValueAsString(ref reader);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }
            catch (JsonException)
            {
                return new(error, message, id);
            }

            return new(error, message, id);
        }

        private static string? ReadNextStringValue(ref Utf8JsonReader reader)
        {
            if (!reader.Read())
            {
                return null;
            }

            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Null => null,
                _ => null
            };
        }

        private static string? ReadNextValueAsString(ref Utf8JsonReader reader)
        {
            if (!reader.Read())
            {
                return null;
            }

            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt64(out var value)
                    ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : null,
                JsonTokenType.Null => null,
                _ => null
            };
        }

        private readonly struct AcgripApiResponse
        {
            public AcgripApiResponse(byte[] raw, AcgripApiResult apiResult)
            {
                Raw = raw;
                ApiResult = apiResult;
            }

            public byte[] Raw { get; }
            public AcgripApiResult ApiResult { get; }

            public string GetRawText() => Raw.Length == 0 ? "" : Encoding.UTF8.GetString(Raw);

            public string GetErrorOrRawText() => ApiResult.HasError ? ApiResult.GetErrorMessage() : GetRawText();
        }

        private readonly struct AcgripApiResult
        {
            public AcgripApiResult(string? error, string? message, string? id)
            {
                Error = error;
                Message = message;
                Id = id;
            }

            public string? Error { get; }
            public string? Message { get; }
            public string? Id { get; }

            public readonly bool HasError => !string.IsNullOrWhiteSpace(Error);

            public readonly string GetErrorMessage()
            {
                if (string.IsNullOrWhiteSpace(Message))
                {
                    return Error ?? "";
                }

                return string.IsNullOrWhiteSpace(Error) ? Message : $"{Error}: {Message}";
            }
        }

        private static HttpResult ParseBrowserPostResult(HttpResponseMessage result, string raw)
        {
            if (result.IsSuccessStatusCode)
            {
                if (raw.Contains("<div class=\"alert alert-warning\">已存在相同的种子</div>"))
                {
                    Log.Information("{Site} has already exist", site);
                    return new(200, "Success", true);
                }
                if (raw.Contains("<div class=\"alert alert-success\">种子发布成功</div>"))
                {
                    Log.Information("{Site} post success", site);
                    return new(200, "Success", true);
                }
                if (raw.Contains("<div class=\"alert alert-warning\">种子内的资源太小</div>"))
                {
                    Log.Error("{Site} upload failed", site);
                    return new(500, "Upload failed, files too small.", false);
                }
                Log.Error("{Site} upload failed. Unknown reson. {NewLine} {Raw}", site, Environment.NewLine, raw);
                return new(500, "Upload failed" + raw, false);
            }
            Log.Error("{Site} upload failed.{NewLine}" +
                "Code: {Code}{NewLine}" +
                "{Raw}", site, Environment.NewLine, result.StatusCode, Environment.NewLine, raw);
            return new((int)result.StatusCode, "Failed" + raw, false);
        }

        private bool Valid()
        {
            if (torrent.Data?.TorrentObject is null)
            {
                Log.Fatal("{Site} torrent.Data?.TorrentObject is null", site);
                throw new ArgumentNullException(nameof(torrent.Data.TorrentObject));
            }

            if (!HasApiToken)
            {
                foreach (var tracker in trackers)
                {
                    if (!torrent.Data.TorrentObject.TrackerTiers.SelectMany(p => p).Any(p => p.TrimEnd('/').Equals(tracker.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
                    {
                        Log.Error("缺少Tracker：{0}", tracker);
                        return false;
                    }
                }
            }
            return ValidTemplate(template, site, torrent.SettingPath);
        }
    }
}
