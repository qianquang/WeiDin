namespace WeiDin.Core.Interfaces;

/// <summary>
/// 文档解析服务 — 从各种文件格式中提取纯文本内容
/// </summary>
public interface IDocumentParser
{
    /// <summary>
    /// 从文件流中提取文本内容
    /// </summary>
    /// <param name="stream">文件流</param>
    /// <param name="fileName">文件名（用于判断格式）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>提取的纯文本内容</returns>
    Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断是否支持该文件格式
    /// </summary>
    bool IsSupported(string fileName);
}
