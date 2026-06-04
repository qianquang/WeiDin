using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Logging;
using WeiDin.Core.Interfaces;

namespace WeiDin.Infrastructure.Services;

/// <summary>
/// 文档解析服务实现 — 支持纯文本、PDF、DOCX 等格式
/// </summary>
public class DocumentParser : IDocumentParser
{
    private readonly ILogger<DocumentParser> _logger;

    // 支持的纯文本格式
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".json", ".xml", ".html", ".htm",
        ".log", ".cs", ".py", ".js", ".ts", ".java", ".cpp", ".c",
        ".h", ".css", ".sql", ".yaml", ".yml", ".toml", ".ini",
        ".cfg", ".conf", ".sh", ".bat", ".ps1", ".rb", ".go", ".rs"
    };

    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf" };
    private static readonly HashSet<string> DocxExtensions = new(StringComparer.OrdinalIgnoreCase) { ".docx" };

    public DocumentParser(ILogger<DocumentParser> logger)
    {
        _logger = logger;
    }

    public bool IsSupported(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return TextExtensions.Contains(ext) || PdfExtensions.Contains(ext) || DocxExtensions.Contains(ext);
    }

    public async Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (TextExtensions.Contains(ext))
            return await ExtractFromTextAsync(stream, cancellationToken);

        if (PdfExtensions.Contains(ext))
            return await ExtractFromPdfAsync(stream, cancellationToken);

        if (DocxExtensions.Contains(ext))
            return await ExtractFromDocxAsync(stream, cancellationToken);

        throw new NotSupportedException($"不支持的文件格式: {ext}");
    }

    /// <summary>
    /// 从纯文本文件提取内容
    /// </summary>
    private async Task<string> ExtractFromTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        var ext = Path.GetExtension("");
        // 如果是 HTML，去除标签
        // Note: 我们没有文件名信息在这里，所以只做基本处理
        return text;
    }

    /// <summary>
    /// 从 PDF 文件提取文本
    /// </summary>
    private Task<string> ExtractFromPdfAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            var sb = new StringBuilder();

            using var document = UglyToad.PdfPig.PdfDocument.Open(stream);
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                    sb.AppendLine(); // 页间分隔
                }
            }

            _logger.LogInformation("PDF 解析完成，提取 {Length} 字符", sb.Length);
            return Task.FromResult(sb.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF 解析失败");
            throw new InvalidOperationException("PDF 文件解析失败，请确保文件未加密且格式正确", ex);
        }
    }

    /// <summary>
    /// 从 DOCX 文件提取文本
    /// </summary>
    private Task<string> ExtractFromDocxAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(stream, false);
            var body = document.MainDocumentPart?.Document?.Body;
            if (body == null)
            {
                return Task.FromResult(string.Empty);
            }

            var sb = new StringBuilder();

            // 遍历所有段落
            foreach (var paragraph in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = paragraph.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
            }

            // 遍历所有表格
            foreach (var table in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var row in table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
                {
                    var cells = row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>()
                        .Select(c => c.InnerText.Trim());
                    sb.AppendLine(string.Join("\t", cells));
                }
            }

            _logger.LogInformation("DOCX 解析完成，提取 {Length} 字符", sb.Length);
            return Task.FromResult(sb.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DOCX 解析失败");
            throw new InvalidOperationException("DOCX 文件解析失败，请确保文件格式正确", ex);
        }
    }

    /// <summary>
    /// 清理 HTML 标签，提取纯文本
    /// </summary>
    public static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        // 去除 HTML 标签
        var text = Regex.Replace(html, "<[^>]+>", " ");
        // 解码 HTML 实体
        text = HttpUtility.HtmlDecode(text);
        // 合并多余空白
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }
}
