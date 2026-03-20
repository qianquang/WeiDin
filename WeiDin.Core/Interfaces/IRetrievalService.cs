using WeiDin.Core.Entities;

namespace WeiDin.Core.Interfaces;

/// <summary>
/// 检索服务接口 - RAG 核心服务
/// </summary>
public interface IRetrievalService
{
    /// <summary>
    /// 根据查询检索相关知识块
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="topK">返回结果数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检索到的知识块列表</returns>
    Task<List<KnowledgeChunk>> RetrieveAsync(string query, int topK = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加知识块到向量库
    /// </summary>
    /// <param name="chunk">知识块</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddChunkAsync(KnowledgeChunk chunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量添加知识块到向量库
    /// </summary>
    /// <param name="chunks">知识块列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddChunksAsync(IEnumerable<KnowledgeChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除知识块
    /// </summary>
    /// <param name="chunkId">知识块ID (Guid格式)</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteChunkAsync(string chunkId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空知识库
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
