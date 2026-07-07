#:property PublishAot=false

using System.Net;
using System.Text;
using System.Text.Json;

const string DefaultBaseUrl = "https://acg.rip/";
const string ApiPath = "api/post";

var token = Environment.GetEnvironmentVariable("ACGRIP_API_TOKEN");
var invalidToken = Environment.GetEnvironmentVariable("ACGRIP_INVALID_TOKEN") ?? "invalid-token-for-okp-probe";
var baseUrl = Environment.GetEnvironmentVariable("ACGRIP_BASE_URL") ?? DefaultBaseUrl;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
{
    PrintHelp();
    return;
}

using var handler = new HttpClientHandler
{
    AllowAutoRedirect = false,
    UseCookies = false,
};

using var client = new HttpClient(handler)
{
    BaseAddress = new Uri(baseUrl),
    Timeout = TimeSpan.FromSeconds(30),
};
client.DefaultRequestHeaders.UserAgent.ParseAdd("OKP-AcgripProbe/1.0");

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine($"BaseUrl: {client.BaseAddress}");
Console.WriteLine($"TokenSource: ACGRIP_API_TOKEN={(string.IsNullOrWhiteSpace(token) ? "<missing>" : Redact(token))}");
Console.WriteLine($"InvalidTokenSource: ACGRIP_INVALID_TOKEN={Redact(invalidToken)}");
Console.WriteLine();

var cases = new TestCase[]
{
    new(
        "no-token/no-body",
        "POST /api/post without token or content. Baseline should usually return NO_TOKEN_PROVIDED.",
        TokenPlacement.None,
        BodyKind.None,
        null),
    new(
        "invalid-header-token/no-body",
        "Header token with a deliberately invalid token. Should usually return INVALID_TOKEN.",
        TokenPlacement.Header,
        BodyKind.None,
        invalidToken),
    new(
        "real-header-token/no-body",
        "Real header token and no body. Useful to see whether token validation reaches field validation.",
        TokenPlacement.Header,
        BodyKind.None,
        token),
    new(
        "real-header-token/empty-multipart",
        "Real header token with empty multipart body. Detects server behavior for empty multipart.",
        TokenPlacement.Header,
        BodyKind.EmptyMultipart,
        token),
    new(
        "real-header-token/fields-no-torrent",
        "Real header token with post fields but no torrent file. Should not publish anything.",
        TokenPlacement.Header,
        BodyKind.FieldsNoTorrentMultipart,
        token),
    new(
        "real-form-token/fields-no-torrent",
        "Token provided as POST field `token`, plus post fields, but no torrent file.",
        TokenPlacement.FormField,
        BodyKind.FieldsNoTorrentMultipart,
        token),
    new(
        "real-cookie-token/fields-no-torrent",
        "Token provided as Cookie `token`, plus post fields, but no torrent file.",
        TokenPlacement.Cookie,
        BodyKind.FieldsNoTorrentMultipart,
        token),
    new(
        "real-header-token/form-urlencoded",
        "Real header token with application/x-www-form-urlencoded fields, no torrent file.",
        TokenPlacement.Header,
        BodyKind.FieldsNoTorrentFormUrlEncoded,
        token),
};

var ran = 0;
var skipped = 0;

foreach (var testCase in cases)
{
    if (RequiresRealToken(testCase) && string.IsNullOrWhiteSpace(testCase.Token))
    {
        skipped++;
        Console.WriteLine($"=== SKIP: {testCase.Name} ===");
        Console.WriteLine("Reason: ACGRIP_API_TOKEN is missing.");
        Console.WriteLine();
        continue;
    }

    ran++;
    await RunCaseAsync(client, testCase);
}

Console.WriteLine("=== Summary ===");
Console.WriteLine($"Ran: {ran}");
Console.WriteLine($"Skipped: {skipped}");
Console.WriteLine("Copy the full output back for judgment. The script redacts configured token values.");

static async Task RunCaseAsync(HttpClient client, TestCase testCase)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, ApiPath);
    AddToken(request, testCase);
    request.Content = BuildContent(testCase);

    Console.WriteLine($"=== CASE: {testCase.Name} ===");
    Console.WriteLine(testCase.Description);
    Console.WriteLine($"TokenPlacement: {testCase.TokenPlacement}");
    Console.WriteLine($"BodyKind: {testCase.BodyKind}");

    var started = DateTimeOffset.UtcNow;
    HttpResponseMessage? response = null;
    byte[] raw = [];

    try
    {
        response = await client.SendAsync(request);
        raw = await response.Content.ReadAsByteArrayAsync();
        var elapsed = DateTimeOffset.UtcNow - started;
        var parsed = ParseApiResponse(raw);

        Console.WriteLine($"Status: {(int)response.StatusCode} {response.ReasonPhrase}");
        Console.WriteLine($"ElapsedMs: {elapsed.TotalMilliseconds:F0}");
        Console.WriteLine($"ContentType: {response.Content.Headers.ContentType?.ToString() ?? "<none>"}");
        Console.WriteLine($"ParsedError: {parsed.Error ?? "<none>"}");
        Console.WriteLine($"ParsedMessage: {parsed.Message ?? "<none>"}");
        Console.WriteLine($"LooksTokenError: {parsed.IsTokenError}");
        Console.WriteLine($"BodyLength: {raw.Length}");
        Console.WriteLine("Body:");
        Console.WriteLine(RedactConfiguredTokens(Encoding.UTF8.GetString(raw)));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Exception: {ex.GetType().FullName}");
        Console.WriteLine(ex.Message);
    }
    finally
    {
        response?.Dispose();
        Console.WriteLine();
    }
}

static bool RequiresRealToken(TestCase testCase)
{
    return testCase.TokenPlacement != TokenPlacement.None
        && testCase.Token != "invalid-token-for-okp-probe"
        && string.IsNullOrWhiteSpace(testCase.Token);
}

static void AddToken(HttpRequestMessage request, TestCase testCase)
{
    if (string.IsNullOrWhiteSpace(testCase.Token))
    {
        return;
    }

    if (testCase.TokenPlacement == TokenPlacement.Header)
    {
        request.Headers.TryAddWithoutValidation("X-API-TOKEN", testCase.Token);
    }
    else if (testCase.TokenPlacement == TokenPlacement.Cookie)
    {
        request.Headers.TryAddWithoutValidation("Cookie", $"token={testCase.Token}");
    }
}

static HttpContent? BuildContent(TestCase testCase)
{
    return testCase.BodyKind switch
    {
        BodyKind.None => null,
        BodyKind.EmptyMultipart => new MultipartFormDataContent(),
        BodyKind.FieldsNoTorrentMultipart => BuildMultipartFields(testCase),
        BodyKind.FieldsNoTorrentFormUrlEncoded => BuildFormUrlEncodedFields(testCase),
        _ => throw new ArgumentOutOfRangeException(nameof(testCase)),
    };
}

static MultipartFormDataContent BuildMultipartFields(TestCase testCase)
{
    var form = new MultipartFormDataContent();
    AddFormTokenIfNeeded(form, testCase);
    form.Add(new StringContent("OKP API probe title"), "post[title]");
    form.Add(new StringContent("9"), "post[category_id]");
    form.Add(new StringContent("0"), "post[series_id]");
    form.Add(new StringContent("OKP API probe content; no torrent file is attached."), "post[content]");
    return form;
}

static FormUrlEncodedContent BuildFormUrlEncodedFields(TestCase testCase)
{
    var fields = new List<KeyValuePair<string, string>>();
    if (testCase.TokenPlacement == TokenPlacement.FormField && !string.IsNullOrWhiteSpace(testCase.Token))
    {
        fields.Add(new("token", testCase.Token));
    }

    fields.Add(new("post[title]", "OKP API probe title"));
    fields.Add(new("post[category_id]", "9"));
    fields.Add(new("post[series_id]", "0"));
    fields.Add(new("post[content]", "OKP API probe content; no torrent file is attached."));
    return new FormUrlEncodedContent(fields);
}

static void AddFormTokenIfNeeded(MultipartFormDataContent form, TestCase testCase)
{
    if (testCase.TokenPlacement == TokenPlacement.FormField && !string.IsNullOrWhiteSpace(testCase.Token))
    {
        form.Add(new StringContent(testCase.Token), "token");
    }
}

static ApiResult ParseApiResponse(ReadOnlySpan<byte> raw)
{
    var reader = new Utf8JsonReader(raw, isFinalBlock: true, state: default);
    string? error = null;
    string? message = null;

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
            else
            {
                reader.Skip();
            }
        }
    }
    catch (JsonException)
    {
        return new(error, message);
    }

    return new(error, message);
}

static string? ReadNextStringValue(ref Utf8JsonReader reader)
{
    if (!reader.Read())
    {
        return null;
    }

    return reader.TokenType switch
    {
        JsonTokenType.String => reader.GetString(),
        JsonTokenType.Null => null,
        _ => null,
    };
}

static string RedactConfiguredTokens(string value)
{
    var result = value;
    var token = Environment.GetEnvironmentVariable("ACGRIP_API_TOKEN");
    var invalidToken = Environment.GetEnvironmentVariable("ACGRIP_INVALID_TOKEN") ?? "invalid-token-for-okp-probe";

    if (!string.IsNullOrWhiteSpace(token))
    {
        result = result.Replace(token, Redact(token), StringComparison.Ordinal);
    }

    if (!string.IsNullOrWhiteSpace(invalidToken))
    {
        result = result.Replace(invalidToken, Redact(invalidToken), StringComparison.Ordinal);
    }

    return result;
}

static string Redact(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "<missing>";
    }

    if (value.Length <= 8)
    {
        return "***";
    }

    return $"{value[..4]}...{value[^4..]}";
}

static void PrintHelp()
{
    Console.WriteLine("ACG.RIP API probe for OKP.");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  $env:ACGRIP_API_TOKEN='1234-abcdefgh...'; dotnet tools/acgrip-api-probe.cs");
    Console.WriteLine();
    Console.WriteLine("Environment variables:");
    Console.WriteLine("  ACGRIP_API_TOKEN      Real token from tpx://acg.rip/<token>.");
    Console.WriteLine("  ACGRIP_INVALID_TOKEN  Optional invalid token used for negative baseline.");
    Console.WriteLine("  ACGRIP_BASE_URL       Optional base URL. Defaults to https://acg.rip/.");
}

enum TokenPlacement
{
    None,
    Header,
    FormField,
    Cookie,
}

enum BodyKind
{
    None,
    EmptyMultipart,
    FieldsNoTorrentMultipart,
    FieldsNoTorrentFormUrlEncoded,
}

readonly record struct TestCase(
    string Name,
    string Description,
    TokenPlacement TokenPlacement,
    BodyKind BodyKind,
    string? Token);

readonly record struct ApiResult(string? Error, string? Message)
{
    public bool IsTokenError => Error?.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) == true;
}
