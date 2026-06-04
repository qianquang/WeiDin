using WeiDin.Core.Enums;

namespace WeiDin.Core.Entities;

/// <summary>
/// 知识条目实体 — 对应一个上传的文件
/// </summary>
public class Knowledge
{
    /// <summary>
    /// 知识条目ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 所属知识库ID
    /// </summary>
    public Guid KnowledgeBaseId { get; set; }

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件存储路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 解析状态
    /// </summary>
    public ParseStatus ParseStatus { get; set; } = ParseStatus.Pending;

    /// <summary>
    /// 失败原因（ParseStatus=Failed 时填写）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 摘要生成状态
    /// </summary>
    public SummaryStatus SummaryStatus { get; set; } = SummaryStatus.None;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 关联的知识库
    /// </summary>
    public virtual KnowledgeBase? KnowledgeBase { get; set; }

    /// <summary>
    /// 该文件产生的分块
    /// </summary>
    public virtual ICollection<KnowledgeChunk> Chunks { get; set; } = new List<KnowledgeChunk>();
}
