using WeiDin.Core.Enums;

namespace WeiDin.Core.Entities;

/// <summary>
/// 知识块实体 - 用于向量检索的基本单元
/// </summary>
public class KnowledgeChunk
{
    /// <summary>
    /// 知识块ID (唯一标识)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 关联的知识库ID
    /// </summary>
    public Guid KnowledgeBaseId { get; set; }

    /// <summary>
    /// 知识块类型
    /// </summary>
    public KnowledgeChunkType Type { get; set; } = KnowledgeChunkType.ChatMessage;

    /// <summary>
    /// 原始文本内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 元数据 JSON 字符串
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// 关联的relationId (如果是聊天消息)
    /// </summary>
    public Guid? RelationId { get; set; }

    /// <summary>
    /// 发送者ID (如果是聊天消息)
    /// </summary>
    public Guid? SenderId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 分块索引 (用于排序)
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// 来源标识 (如群组ID、用户ID等)
    /// </summary>
    public string? SourceId { get; set; }

    /// <summary>
    /// 向量维度 (缓存用)
    /// </summary>
    public int? VectorDimension { get; set; }

    /// <summary>
    /// 向量数据 (用于持久化存储)
    /// </summary>
    public byte[]? Vector { get; set; }

    /// <summary>
    /// 关联的知识库
    /// </summary>
    public virtual KnowledgeBase? KnowledgeBase { get; set; }
}
