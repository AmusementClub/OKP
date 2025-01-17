using Serilog;
using Markdig;
using Converter.MarkdownToBBCode.Shared;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Parsers;
using OKP.Core.Utils;

namespace OKP.Core.Interface
{
    internal abstract class AdapterBase
    {
        public abstract Task<HttpResult> PingAsync();
        public abstract Task<HttpResult> PostAsync();

        internal static string ConvertMarkdownToHtml(string mdContent)
        {
            var pipeline = new MarkdownPipelineBuilder().UseSoftlineBreakAsHardlineBreak().Build();
            return Markdown.ToHtml(mdContent, pipeline);
        }

        internal static string ConvertMarkdownToBBCode(string mdContent)
        {
            var pipeline = new MarkdownPipelineBuilder().EnableTrackTrivia().UseEmphasisExtras(EmphasisExtraOptions.Strikethrough).UseSoftlineBreakAsHardlineBreak().Build();
            var document = MarkdownParser.Parse(mdContent, pipeline);

            using var sw = new StringWriter();
            var renderer = new BBCodeRenderer(BBCodeType.NexusMods, pipeline, false, false, sw);
            renderer.Render(document);
            renderer.Writer.Flush();

            return renderer.Writer.ToString() ?? string.Empty;
        }

        private enum ContentType
        {
            Text,
            MarkdownFile,
            HtmlFile,
            BBCodeFile,
        }

        private static string GetContentExt(ContentType contentType)
        {
            return contentType switch
            {
                ContentType.MarkdownFile => ".md",
                ContentType.HtmlFile => ".html",
                ContentType.BBCodeFile => ".bbcode",
                _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, null)
            };
        }

        private static ContentType GetContentType(string site)
        {
            return site switch
            {
                "dmhy" => ContentType.HtmlFile,
                "acgnx_asia" => ContentType.HtmlFile,
                "acgnx_global" => ContentType.HtmlFile,
                "acgrip" => ContentType.BBCodeFile,
                "bangumi" => ContentType.HtmlFile,
                "nyaa" => ContentType.MarkdownFile,
                _ => throw new ArgumentOutOfRangeException(nameof(site), site, null)
            };
        }

        internal static bool ValidTemplate(TorrentContent.Template template, string site, string? settingPath)
        {
            if (template.Content == null) return false;
            var contentType = ContentType.Text;

            var span = template.Content.AsSpan();
            if (span.IndexOf('\n') != -1) return true;

            if (span.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                contentType = ContentType.MarkdownFile;
            }
            else if (span.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                contentType = ContentType.HtmlFile;
            }
            else if (span.EndsWith(".bbcode", StringComparison.OrdinalIgnoreCase))
            {
                contentType = ContentType.BBCodeFile;
            }
            else
            {
                return true;
            }

            var siteContentType = GetContentType(site);
            var siteContentExt = GetContentExt(siteContentType);
            var contentExt = GetContentExt(contentType);
            if (contentType != siteContentType)
            {
                Log.Debug("不存在{Site} {FileExt}文件，将用 {File} 生成", site, siteContentExt, template.Content);
            }
            else
            {
                Log.Debug("开始寻找{Site} {FileExt}文件 {File}", site, siteContentExt, template.Content);
            }
            var templateFile = FileHelper.ParseFileFullPath(template.Content, settingPath);
            if (File.Exists(templateFile))
            {
                Log.Debug("找到了{FileExt}文件 {File}", contentExt, templateFile);
                var text = File.ReadAllText(templateFile);
                if (contentType == siteContentType)
                {
                    template.Content = text;
                }
                else
                {
                    template.Content = siteContentType switch
                    {
                        ContentType.HtmlFile => ConvertMarkdownToHtml(text),
                        ContentType.BBCodeFile => ConvertMarkdownToBBCode(text),
                        _ => throw new ArgumentOutOfRangeException()
                    };
                }
            }
            else
            {
                Log.Error("发布模板不存在{NewLine}{Source}-->{Dest}", Environment.NewLine, template.Content, templateFile);
                return false;
            }

            return true;
        }
    }
    public class HttpResult
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public bool IsSuccess { get; set; }
        public HttpResult(int code, string message, bool isSuccess)
        {
            Code = code;
            Message = message;
            IsSuccess = isSuccess;
        }
    }
}
