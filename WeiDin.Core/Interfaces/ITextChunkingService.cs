namespace WeiDin.Core.Interfaces;

/// <summary>
/// 文本分块服务 — 将长文本按语义边界切分为适合向量化的小段
/// </summary>
public interface ITextChunkingService
{
    /// <summary>
    /// 将文本切分为多个分块
    /// </summary>
    /// <param name="text">原始文本</param>
    /// <param name="chunkSize">每个分块的最大字符数</param>
    /// <param name="overlap">分块之间的重叠字符数</param>
    /// <returns>切分后的文本列表</returns>
    List<string> Chunk(string text, int chunkSize = 500, int overlap = 50);
}
