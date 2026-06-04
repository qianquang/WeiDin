using WeiDin.Core.Enums;

namespace WeiDin.Core.Entities;

/// <summary>
/// 知识库实体
/// </summary>
public class KnowledgeBase
{
    /// <summary>
    /// 知识库ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 知识库名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 知识库描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 索引策略
    /// </summary>
    public IndexingStrategy IndexingStrategy { get; set; } = IndexingStrategy.Vector;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 知识块数量
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 关联的知识条目
    /// </summary>
    public virtual ICollection<Knowledge> Knowledge { get; set; } = new List<Knowledge>();

    /// <summary>
    /// 关联的知识块
    /// </summary>
    public virtual ICollection<KnowledgeChunk> Chunks { get; set; } = new List<KnowledgeChunk>();
}
