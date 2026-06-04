using Microsoft.Extensions.VectorData;

namespace WeiDin.Core.Models;

/// <summary>
/// 向量存储记录模型 — 存储在向量数据库中的 Chunk 数据
/// 每个知识库对应一个 collection，名称 = $"kb_{knowledgeBaseId}"
/// </summary>
public class ChunkVectorRecord
{
    /// <summary>
    /// 记录主键（Chunk.Id.ToString()）
    /// </summary>
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 所属知识库ID（用于按知识库过滤）
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public string KnowledgeBaseId { get; set; } = string.Empty;

    /// <summary>
    /// 知识条目ID（用于关联 Knowledge 实体）
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public string KnowledgeId { get; set; } = string.Empty;

    /// <summary>
    /// Chunk 原文内容（向量搜索结果直接包含，减少 DB 回查）
    /// </summary>
    [VectorStoreData]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 向量嵌入（512 维，BGE-small-zh-v1.5）
    /// </summary>
    [VectorStoreVector(512, IndexKind = IndexKind.Hnsw, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}
