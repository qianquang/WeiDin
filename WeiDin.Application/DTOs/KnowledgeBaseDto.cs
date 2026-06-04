using WeiDin.Core.Enums;

namespace WeiDin.Application.DTOs;

// ========== 知识库 DTOs ==========

public class KnowledgeBaseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ChunkCount { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateKnowledgeBaseDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateKnowledgeBaseDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsEnabled { get; set; }
}

// ========== 知识块 DTOs ==========

public class KnowledgeChunkDto
{
    public Guid Id { get; set; }
    public Guid KnowledgeBaseId { get; set; }
    public KnowledgeChunkType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public Guid? RelationId { get; set; }
    public Guid? SenderId { get; set; }
    public int ChunkIndex { get; set; }
    public string? SourceId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AddChunkDto
{
    public KnowledgeChunkType Type { get; set; } = KnowledgeChunkType.Custom;
    public string Content { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public string? SourceId { get; set; }
}

public class AddChunksBatchDto
{
    public List<AddChunkDto> Chunks { get; set; } = new();
}

public class UploadDocumentDto
{
    public KnowledgeChunkType Type { get; set; } = KnowledgeChunkType.Custom;
    public string? SourceId { get; set; }
    /// <summary>
    /// 每个分块的最大字符数，默认 500
    /// </summary>
    public int ChunkSize { get; set; } = 500;
    /// <summary>
    /// 分块之间的重叠字符数，默认 50
    /// </summary>
    public int ChunkOverlap { get; set; } = 50;
}

public class RetrieveQueryDto
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
}
