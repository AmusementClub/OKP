// ReSharper disable InconsistentNaming
using OKP.Core.Utils;
using Serilog;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OKP.Core.Interface.Anibt
{
    // 标题 -> bgmid 自动匹配。一条代码路径吃中/英/日/罗马音/译名：不按语言分规则，
    // 而是把标题和候选条目的全部别名（name + name_cn + infobox 别名）做模糊相似度取最大。
    // 站点侧不做匹配，所以直连 api.bgm.tv（v0 搜索带 infobox 别名和放送日期，anibt 的
    // bgm/search 没有别名、date 恒为 null，不够打分用）。
    internal static partial class BangumiMatcher
    {
        private static readonly Uri baseUrl = new("https://api.bgm.tv/");
        private const string userAgent = "bangumi2anibt/1.0 (https://github.com/AmusementClub/OKP)";

        // 置信度门限：调这几个数就能在“少人工确认”和“更保守”之间权衡
        private const double acceptScore = 0.90;   // 命中别名至少这么像
        private const double acceptMargin = 0.06;  // 且比第二名领先这么多
        private const double strongScore = 0.985;  // 近乎精确：无视 margin 直接接受
        private const double rejectScore = 0.50;   // 低于此当噪声，直接 no_match 不打扰人

        public enum MatchStatus
        {
            NoMatch,
            Matched,
            Ambiguous
        }

        public class Candidate
        {
            public int BgmId;
            public string? Name;
            public string? NameCn;
            public string? Date;
            public double Score;    // 各语言段里最好的那个命中（0..1）
            public double Sum;      // 各语言段最好命中之和：匹配得越全越大，用于同分消歧
            public string? Via;     // 命中的是哪个标题变体
        }

        public class MatchResult
        {
            public MatchStatus Status;
            public int BgmId;
            public string? Name;    // 选中条目的日文原名
            public string? NameCn;  // 选中条目的中文名
            public double Score;
            public double Margin;
            public List<Candidate> Candidates = [];
        }

        // title：发布标题。字幕组标题通常是「[群组] 中文 / Romaji / 日文 1080p HEVC BDRip [S1]」，
        // 会先拆成各语言段（去群组/发布 token）再逐段匹配。同名多季/合集打平时，按 anibt 站长的规则
        // 取最新一季的 bgmid。
        public static async Task<MatchResult> MatchAsync(string title, string? proxy)
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            if (proxy is not null)
            {
                handler.Proxy = new WebProxy(new Uri(proxy), BypassOnLocal: false);
            }
            using var client = new HttpClient(handler) { BaseAddress = baseUrl };
            client.DefaultRequestHeaders.Add("user-agent",
                string.IsNullOrWhiteSpace(HttpHelper.GlobalUserAgent) ? userAgent : HttpHelper.GlobalUserAgent);

            var segments = Segments(title);
            var queries = segments.Count > 0 ? segments : [title];

            // 召回并去重：每个语言段各搜一次（段是干净的单语言名，命中率远高于整条噪声标题）
            var subjects = new Dictionary<int, BgmSubject>();
            foreach (var seg in queries.Take(3))
            {
                await RecallInto(client, seg, subjects);
            }

            return Decide(title, queries, subjects.Values, IsCollection(title));
        }

        private static async Task RecallInto(HttpClient client, string keyword, Dictionary<int, BgmSubject> into)
        {
            try
            {
                var req = new BgmSearchRequest { keyword = keyword, filter = new() { type = [2] } };
                // limit=20 是 v0 的上限；别名命中有时排到第十几位，10 会被切掉
                var resp = await client.PostAsJsonAsyncWithRetry("v0/search/subjects?limit=20", req,
                    BgmMatcherSourceGenerationContext.Default.BgmSearchRequest, setCookie: false);
                if (!resp.IsSuccessStatusCode) return;
                var body = await resp.Content.ReadFromJsonAsync(BgmMatcherSourceGenerationContext.Default.BgmSearchResponse);
                foreach (var s in body?.data ?? [])
                {
                    into.TryAdd(s.id, s); // 先到（排名靠前）的优先
                }
            }
            catch (Exception ex)
            {
                Log.Debug("bgm 搜索失败 {Keyword}: {Msg}", keyword, ex.Message);
            }
        }

        private static MatchResult Decide(string rawTitle, IReadOnlyList<string> queries, IEnumerable<BgmSubject> subjectsEnum, bool isCollection)
        {
            var subjects = subjectsEnum.ToList();
            var result = new MatchResult();
            if (subjects.Count == 0) return result;

            var scored = subjects
                .Select(s =>
                {
                    var (score, sum, via) = ScoreSubject(queries, s);
                    return new Candidate { BgmId = s.id, Name = s.name, NameCn = s.name_cn, Date = s.date, Score = score, Sum = sum, Via = via };
                })
                .OrderByDescending(c => c.Score)
                .ThenByDescending(c => c.Sum)
                .ToList();

            var top = scored[0];
            var second = scored.Count > 1 ? scored[1].Score : 0.0;

            // 某段别名逐字相同（同名多季常共用英文名）会让多个候选都到满分。此时先比"匹配得更全"
            // （各段命中之和 Sum）——单季发布 [S1] 三段都命中它自己，胜过只共用英文名的续作；只有
            // Sum 也相等（真·无法区分）才用站长的"取最新一季"。这样单季精确、真歧义才落最新。
            var exactTie = scored.Where(c => top.Score - c.Score < 1e-6).ToList();
            if (exactTie.Count > 1)
            {
                top = exactTie.OrderByDescending(c => c.Sum).ThenByDescending(c => DateKey(c.Date)).First();
            }

            // 合集（标题含季度区间 S1-4 / 第1-4季 / + OVA 等）→ anibt 站长的规则：用最新一季的 bgmid，
            // 但“最新”是合集覆盖到的最高季，不是全系列的最新季（[S1-S2] 合集要 S2，不是已出的 S4）。
            // 在“同系列”候选里，取不超过标题季度上限的最高季（同季再取放送最新）。
            if (isCollection)
            {
                var maxSeason = MaxSeason(rawTitle);
                var baseNames = queries.Select(Normalize).Where(b => b.Length >= 2).ToList();
                var franchise = scored.Where(c =>
                {
                    if (c.Score < 0.85) return false;
                    var cn = Normalize(c.NameCn ?? c.Name ?? "");
                    return baseNames.Any(b => cn.StartsWith(b, StringComparison.Ordinal)
                                           || b.StartsWith(cn, StringComparison.Ordinal));
                }).ToList();
                var inRange = franchise.Where(c => SeasonNum(c.NameCn ?? c.Name ?? "") <= maxSeason).ToList();
                var pool = inRange.Count > 0 ? inRange : franchise;
                if (pool.Count > 0)
                {
                    top = pool.OrderByDescending(c => SeasonNum(c.NameCn ?? c.Name ?? ""))
                              .ThenByDescending(c => DateKey(c.Date)).First();
                }
            }

            result.BgmId = top.BgmId;
            result.Name = top.Name;
            result.NameCn = top.NameCn;
            result.Score = top.Score;
            result.Margin = top.Score - second;
            // 选中的排在候选首位，其余按分数序补齐（日期消歧可能把 top 换成非分数第一）
            result.Candidates = scored.Where(c => c.BgmId == top.BgmId)
                .Concat(scored.Where(c => c.BgmId != top.BgmId)).Take(5).ToList();
            result.Status = top.Score >= strongScore || (top.Score >= acceptScore && result.Margin >= acceptMargin)
                ? MatchStatus.Matched
                : top.Score < rejectScore
                    ? MatchStatus.NoMatch
                    : MatchStatus.Ambiguous;
            return result;
        }

        // 每个查询段 × 每个别名。score = 全局最大（干净段能精确命中拿 1.0）；sum = 每段最好命中之和
        // （匹配得越全越大，用于同分消歧，区分"三段全中的本季"和"只共用英文名的续作"）。
        private static (double score, double sum, string? via) ScoreSubject(IReadOnlyList<string> queries, BgmSubject s)
        {
            var variants = Variants(s).ToList();
            var max = 0.0;
            var sum = 0.0;
            string? via = null;
            foreach (var q in queries)
            {
                var bestForSeg = 0.0;
                foreach (var v in variants)
                {
                    var sc = Similarity(q, v);
                    if (sc > bestForSeg) bestForSeg = sc;
                    if (sc > max)
                    {
                        max = sc;
                        via = v;
                    }
                }
                sum += bestForSeg;
            }
            return (max, sum, via);
        }

        // 条目的全部标题变体：name + name_cn + infobox 别名（value 可能是字符串或 [{v}] 数组）
        private static IEnumerable<string> Variants(BgmSubject s)
        {
            if (!string.IsNullOrWhiteSpace(s.name)) yield return s.name;
            if (!string.IsNullOrWhiteSpace(s.name_cn)) yield return s.name_cn;
            foreach (var f in s.infobox ?? [])
            {
                if (f.key != "别名") continue;
                if (f.value.ValueKind == JsonValueKind.String)
                {
                    var str = f.value.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) yield return str;
                }
                else if (f.value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in f.value.EnumerateArray())
                    {
                        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("v", out var v) &&
                            v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
                        {
                            yield return v.GetString()!;
                        }
                    }
                }
            }
        }

        private static double Similarity(string query, string title)
        {
            var a = Normalize(query);
            var b = Normalize(title);
            if (a.Length == 0 || b.Length == 0) return 0.0;
            if (a == b) return 1.0;

            var dist = Levenshtein(a, b);
            var ratio = 1.0 - (double)dist / Math.Max(a.Length, b.Length);

            // 包含：短的完整落在长的里面（"EVA" 在长别名里、" 劇場版" 后缀等）。按长度占比缩放。
            var (sh, lo) = a.Length <= b.Length ? (a, b) : (b, a);
            if (lo.Contains(sh, StringComparison.Ordinal))
            {
                var floor = 0.85 + 0.15 * ((double)sh.Length / lo.Length);
                if (floor > ratio) ratio = floor;
            }
            return ratio;
        }

        // NFKC（全角->半角、片假名宽度）+ 小写 + 片假名->平假名 + 去分隔符/标点。语言无关，
        // 把英文/罗马音/假名/汉字落到可比对的同一面。
        private static string Normalize(string s)
        {
            s = s.Normalize(NormalizationForm.FormKC);
            var sb = new StringBuilder(s.Length);
            foreach (var raw in s)
            {
                var c = char.ToLowerInvariant(raw);
                if (c >= 0x30A1 && c <= 0x30F6) c = (char)(c - 0x60); // 片假名 -> 平假名
                if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c)) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static int Levenshtein(string a, string b)
        {
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++) prev[j] = j;
            for (var i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + cost);
                }
                (prev, cur) = (cur, prev);
            }
            return prev[b.Length];
        }

        // 把发布标题拆成干净的单语言段：去 [群组]/【】/() → 按 / ／ | 分段 → 每段砍掉发布
        // token 尾巴（分辨率/编码/来源/BDRip/集数标记等，从第一个 token 一直到结尾）。
        // 季度标记（第二季 / 2nd / 数字后缀）保留在段里，用于匹配正确的那一季。
        private static List<string> Segments(string title)
        {
            var noBracket = BracketRegex().Replace(title, " ");
            var segs = new List<string>();
            foreach (var part in noBracket.Split('/', '／', '|', '\\'))
            {
                var s = WsRegex().Replace(ReleaseTailRegex().Replace(part, " ").Trim(), " ");
                if (s.Length >= 2 && !segs.Contains(s))
                {
                    segs.Add(s);
                }
            }
            return segs;
        }

        [GeneratedRegex(@"\[[^\]]*\]|【[^】]*】|\([^\)]*\)|（[^）]*）")]
        private static partial Regex BracketRegex();

        // 从第一个发布 token 起，到结尾一律砍掉（发布信息一般都在标题尾部）
        [GeneratedRegex(@"\s+(10-?bit|8-?bit|4k|2160p|1080p|720p|480p|hevc|avc|av1|x264|x265|h\.?264|h\.?265|flac|aac|opus|ma10p|hi10p|bdrip|bd|webrip|web-?dl|hdtv|blu-?ray|reseed|fin|end|oad|ova|movie|live|tv|sp)\b.*$",
            RegexOptions.IgnoreCase)]
        private static partial Regex ReleaseTailRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WsRegex();

        // 标题是否是跨季合集：含季度区间（S1-4 / S1-S4 / 第1-4季）或聚合标记（+ OVA/Movie/S2…）
        private static bool IsCollection(string title) => CollectionRegex().IsMatch(title);

        [GeneratedRegex(@"S\d+\s*[-~～]\s*S?\d+|第?\s*\d+\s*[-~～]\s*\d+\s*[季期]|\+\s*(OVA|OAD|OAV|Movie|SP|Season|S\d|剧场版)",
            RegexOptions.IgnoreCase)]
        private static partial Regex CollectionRegex();

        // 一个条目名里的季度号：第X季/期、Nnd Season、Season N、罗马数字 Ⅱ/Ⅲ/Ⅳ；识别不到当第 1 季。
        private static int SeasonNum(string name)
        {
            var cn = SeasonCnRegex().Match(name);
            if (cn.Success)
            {
                var n = CnNumeral(cn.Groups[1].Value);
                if (n > 0) return n;
            }
            var en = SeasonEnRegex().Match(name);
            if (en.Success)
            {
                var g = en.Groups[1].Success ? en.Groups[1].Value : en.Groups[2].Value;
                if (int.TryParse(g, out var n) && n > 0) return n;
            }
            if (name.Contains('Ⅳ') || name.Contains('Ⅳ')) return 4;
            if (name.Contains('Ⅲ')) return 3;
            if (name.Contains('Ⅱ')) return 2;
            return 1;
        }

        // 标题覆盖到的最高季：区间取上界（S1-S4→4），否则取出现过的最大季度号；识别不到当 1。
        private static int MaxSeason(string title)
        {
            var max = 1;
            foreach (Match m in SeasonAnyRegex().Matches(title))
            {
                for (var g = 1; g < m.Groups.Count; g++)
                {
                    if (!m.Groups[g].Success) continue;
                    var n = char.IsDigit(m.Groups[g].Value[0]) ? int.Parse(m.Groups[g].Value) : CnNumeral(m.Groups[g].Value);
                    if (n > max) max = n;
                }
            }
            return max;
        }

        private static int CnNumeral(string s)
        {
            if (int.TryParse(s, out var d)) return d;
            const string digits = "〇一二三四五六七八九";
            if (s.Length == 1)
            {
                if (s[0] == '十') return 10;
                var i = digits.IndexOf(s[0]);
                return i > 0 ? i : 0;
            }
            // 十X / X十 / X十Y（覆盖到 ~99，季度够用）
            var ten = s.IndexOf('十');
            if (ten < 0) return 0;
            var high = ten == 0 ? 1 : digits.IndexOf(s[0]);
            var low = ten == s.Length - 1 ? 0 : digits.IndexOf(s[^1]);
            return high < 0 || low < 0 ? 0 : high * 10 + low;
        }

        [GeneratedRegex(@"第\s*([一二三四五六七八九十〇\d]+)\s*[季期]")]
        private static partial Regex SeasonCnRegex();

        [GeneratedRegex(@"\b(\d+)(?:st|nd|rd|th)\s*Season\b|\bSeason\s*(\d+)\b", RegexOptions.IgnoreCase)]
        private static partial Regex SeasonEnRegex();

        // 任意季度出现（含区间上界）：抓所有数字/中文数字季度号，MaxSeason 取最大
        [GeneratedRegex(@"[Ss]\d*\s*[-~～]\s*[Ss]?(\d+)|第?\s*\d+\s*[-~～]\s*([一二三四五六七八九十〇\d]+)\s*[季期]|第\s*([一二三四五六七八九十〇\d]+)\s*[季期]|\b[Ss](\d+)\b|\b(\d+)(?:st|nd|rd|th)\s*Season\b")]
        private static partial Regex SeasonAnyRegex();

        // "YYYY-MM-DD"/"YYYY-MM"/"YYYY" -> YYYYMMDD（缺月/日补 0），0 表示无年份
        private static long DateKey(string? d)
        {
            if (string.IsNullOrEmpty(d) || d.Length < 4) return 0;
            if (!int.TryParse(d.AsSpan(0, 4), out var y)) return 0;
            var m = d.Length >= 7 && int.TryParse(d.AsSpan(5, 2), out var mm) ? mm : 0;
            var day = d.Length >= 10 && int.TryParse(d.AsSpan(8, 2), out var dd) ? dd : 0;
            return y * 10000L + m * 100 + day;
        }

#pragma warning disable IDE1006
        internal class BgmSearchRequest
        {
            public string keyword { get; set; } = "";
            public BgmSearchFilter filter { get; set; } = new();
        }

        internal class BgmSearchFilter
        {
            public int[] type { get; set; } = [];
        }

        internal class BgmSearchResponse
        {
            public List<BgmSubject>? data { get; set; }
        }

        internal class BgmSubject
        {
            public int id { get; set; }
            public string? name { get; set; }
            public string? name_cn { get; set; }
            public string? date { get; set; }
            public List<BgmInfobox>? infobox { get; set; }
        }

        internal class BgmInfobox
        {
            public string? key { get; set; }
            public JsonElement value { get; set; }
        }
#pragma warning restore IDE1006
    }

    [JsonSerializable(typeof(BangumiMatcher.BgmSearchRequest))]
    [JsonSerializable(typeof(BangumiMatcher.BgmSearchResponse))]
    [JsonSourceGenerationOptions(NumberHandling = JsonNumberHandling.AllowReadingFromString)]
    internal partial class BgmMatcherSourceGenerationContext : JsonSerializerContext;
}
