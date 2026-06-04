using WeiDin.Core.Enums;

namespace WeiDin.Application.DTOs;

/// <summary>
/// 文档上传请求 DTO
/// </summary>
public class UploadKnowledgeDocumentDto
{
    /// <summary>
    /// 所属知识库ID
    /// </summary>
    public Guid KnowledgeBaseId { get; set; }

    /// <summary>
    /// 知识块类型
    /// </summary>
    public KnowledgeChunkType Type { get; set; } = KnowledgeChunkType.Custom;
}

/// <summary>
/// 文档处理状态响应 DTO
/// </summary>
public class DocumentIngestionStatusDto
{
    /// <summary>
    /// 知识条目ID
    /// </summary>
    public Guid KnowledgeId { get; set; }

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 解析状态
    /// </summary>
    public ParseStatus ParseStatus { get; set; }

    /// <summary>
    /// 失败原因
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 已生成的分块数量
    /// </summary>
    public int ChunkCount { get; set; }
}
