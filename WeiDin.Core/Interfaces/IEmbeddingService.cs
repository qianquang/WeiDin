namespace WeiDin.Core.Interfaces;

/// <summary>
/// 向量嵌入服务接口
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// 将文本转换为向量嵌入
    /// </summary>
    /// <param name="text">输入文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>向量嵌入数组</returns>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量将文本转换为向量嵌入
    /// </summary>
    /// <param name="texts">输入文本列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>向量嵌入数组列表</returns>
    Task<float[][]> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取向量维度
    /// </summary>
    int Dimension { get; }
}
