using WeiDin.Core.Entities;

namespace WeiDin.Core.Interfaces;

/// <summary>
/// 检索服务接口 — 通过 SK 向量存储进行检索
/// </summary>
public interface IRetrievalService
{
    /// <summary>
    /// 根据查询检索相关知识块
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="knowledgeBaseId">知识库ID</param>
    /// <param name="topK">返回结果数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<List<KnowledgeChunk>> RetrieveAsync(string query, Guid knowledgeBaseId, int topK = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加知识块到向量存储
    /// </summary>
    /// <param name="chunk">知识块结构化数据</param>
    /// <param name="vector">向量数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddChunkAsync(KnowledgeChunk chunk, float[] vector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量添加知识块到向量存储
    /// </summary>
    /// <param name="chunks">知识块和向量的元组列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddChunksAsync(IEnumerable<(KnowledgeChunk chunk, float[] vector)> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除知识块
    /// </summary>
    /// <param name="chunkId">知识块ID</param>
    /// <param name="knowledgeBaseId">知识库ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteChunkAsync(Guid chunkId, Guid knowledgeBaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空知识库的所有向量
    /// </summary>
    /// <param name="knowledgeBaseId">知识库ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearAsync(Guid knowledgeBaseId, CancellationToken cancellationToken = default);
}
