using Microsoft.Extensions.Logging;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces.Knowledge;
using WeiDin.Core.Entities;
using WeiDin.Core.Enums;
using WeiDin.Core.Interfaces;
using WeiDin.Infrastructure.Repositories;

namespace WeiDin.Application.Services.Knowledge;

public class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly KnowledgeBaseRepository _kbRepository;
    private readonly KnowledgeChunkRepository _chunkRepository;
    private readonly IRetrievalService _retrievalService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ITextChunkingService _chunkingService;
    private readonly ILogger<KnowledgeBaseService> _logger;

    public KnowledgeBaseService(
        KnowledgeBaseRepository kbRepository,
        KnowledgeChunkRepository chunkRepository,
        IRetrievalService retrievalService,
        IEmbeddingService embeddingService,
        ITextChunkingService chunkingService,
        ILogger<KnowledgeBaseService> logger)
    {
        _kbRepository = kbRepository;
        _chunkRepository = chunkRepository;
        _retrievalService = retrievalService;
        _embeddingService = embeddingService;
        _chunkingService = chunkingService;
        _logger = logger;
    }

    // ========== 知识库 CRUD ==========

    public async Task<KnowledgeBaseDto> CreateAsync(CreateKnowledgeBaseDto input)
    {
        var entity = new KnowledgeBase
        {
            Id = Guid.NewGuid(),
            Name = input.Name,
            Description = input.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _kbRepository.AddAsync(entity);
        _logger.LogInformation("知识库创建成功: {Id}, 名称: {Name}", entity.Id, entity.Name);

        return MapToDto(entity);
    }

    public async Task<List<KnowledgeBaseDto>> GetAllAsync()
    {
        var list = await _kbRepository.GetAllAsync();
        return list.Select(MapToDto).ToList();
    }

    public async Task<KnowledgeBaseDto?> GetByIdAsync(Guid id)
    {
        var entity = await _kbRepository.GetByIdAsync(id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<KnowledgeBaseDto?> UpdateAsync(Guid id, UpdateKnowledgeBaseDto input)
    {
        var entity = await _kbRepository.GetByIdAsync(id);
        if (entity == null) return null;

        if (input.Name != null) entity.Name = input.Name;
        if (input.Description != null) entity.Description = input.Description;
        if (input.IsEnabled.HasValue) entity.IsEnabled = input.IsEnabled.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await _kbRepository.UpdateAsync(entity);
        _logger.LogInformation("知识库更新成功: {Id}", id);

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _kbRepository.GetByIdAsync(id);
        if (entity == null) return false;

        // 清空向量存储
        await _retrievalService.ClearAsync(id);

        await _kbRepository.DeleteAsync(entity);
        _logger.LogInformation("知识库删除成功: {Id}", id);
        return true;
    }

    // ========== 知识块管理 ==========

    public async Task<KnowledgeChunkDto> AddChunkAsync(Guid knowledgeBaseId, AddChunkDto input)
    {
        var kb = await _kbRepository.GetByIdAsync(knowledgeBaseId);
        if (kb == null) throw new InvalidOperationException("知识库不存在");

        var chunk = new KnowledgeChunk
        {
            Id = Guid.NewGuid(),
            KnowledgeBaseId = knowledgeBaseId,
            Type = input.Type,
            Content = input.Content,
            Metadata = input.Metadata,
            SourceId = input.SourceId,
            CreatedAt = DateTime.UtcNow
        };

        // 生成向量
        var vector = await _embeddingService.EmbedAsync(chunk.Content);

        // 向量写入 SK VectorStore
        await _retrievalService.AddChunkAsync(chunk, vector);

        // 结构化数据写入 SQL Server
        await _chunkRepository.AddAsync(chunk);
        await _kbRepository.UpdateChunkCountAsync(knowledgeBaseId);

        _logger.LogInformation("知识块添加成功: {ChunkId}, 知识库: {KbId}", chunk.Id, knowledgeBaseId);
        return MapToChunkDto(chunk);
    }

    public async Task<List<KnowledgeChunkDto>> AddChunksBatchAsync(Guid knowledgeBaseId, AddChunksBatchDto input)
    {
        var kb = await _kbRepository.GetByIdAsync(knowledgeBaseId);
        if (kb == null) throw new InvalidOperationException("知识库不存在");

        var chunks = input.Chunks.Select((c, i) => new KnowledgeChunk
        {
            Id = Guid.NewGuid(),
            KnowledgeBaseId = knowledgeBaseId,
            Type = c.Type,
            Content = c.Content,
            Metadata = c.Metadata,
            SourceId = c.SourceId,
            ChunkIndex = i,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        // 批量生成向量
        var texts = chunks.Select(c => c.Content).ToList();
        var vectors = await _embeddingService.EmbedBatchAsync(texts);

        // 向量写入 SK VectorStore
        var pairs = chunks.Zip(vectors, (chunk, vector) => (chunk, vector)).ToList();
        await _retrievalService.AddChunksAsync(pairs);

        // 结构化数据写入 SQL Server
        await _chunkRepository.AddRangeAsync(chunks);
        await _kbRepository.UpdateChunkCountAsync(knowledgeBaseId);

        _logger.LogInformation("批量添加 {Count} 个知识块到知识库 {KbId}", chunks.Count, knowledgeBaseId);
        return chunks.Select(MapToChunkDto).ToList();
    }

    public async Task<List<KnowledgeChunkDto>> GetChunksAsync(Guid knowledgeBaseId, int page = 1, int pageSize = 50)
    {
        var allChunks = await _chunkRepository.GetByKnowledgeBaseIdAsync(knowledgeBaseId);
        return allChunks
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToChunkDto)
            .ToList();
    }

    public async Task<bool> DeleteChunkAsync(Guid knowledgeBaseId, Guid chunkId)
    {
        var chunk = await _chunkRepository.GetByIdAsync(chunkId);
        if (chunk == null || chunk.KnowledgeBaseId != knowledgeBaseId) return false;

        // 从 SK VectorStore 删除
        await _retrievalService.DeleteChunkAsync(chunkId, knowledgeBaseId);

        // 从 SQL Server 删除
        await _chunkRepository.DeleteAsync(chunk);
        await _kbRepository.UpdateChunkCountAsync(knowledgeBaseId);

        _logger.LogInformation("知识块删除成功: {ChunkId}", chunkId);
        return true;
    }

    public async Task<int> ClearChunksAsync(Guid knowledgeBaseId)
    {
        var chunks = await _chunkRepository.GetByKnowledgeBaseIdAsync(knowledgeBaseId);
        var count = chunks.Count;

        // 清空向量存储
        await _retrievalService.ClearAsync(knowledgeBaseId);

        // 清空 SQL Server
        foreach (var chunk in chunks)
        {
            await _chunkRepository.DeleteAsync(chunk);
        }

        await _kbRepository.UpdateChunkCountAsync(knowledgeBaseId);
        _logger.LogInformation("清空知识库 {KbId} 的 {Count} 个知识块", knowledgeBaseId, count);
        return count;
    }

    // ========== 文档上传（文本分块） ==========

    public async Task<List<KnowledgeChunkDto>> UploadTextAsync(Guid knowledgeBaseId, string text, UploadDocumentDto input)
    {
        var kb = await _kbRepository.GetByIdAsync(knowledgeBaseId);
        if (kb == null) throw new InvalidOperationException("知识库不存在");

        var textChunks = _chunkingService.Chunk(text, input.ChunkSize, input.ChunkOverlap);
        var chunks = new List<KnowledgeChunk>();

        for (int i = 0; i < textChunks.Count; i++)
        {
            chunks.Add(new KnowledgeChunk
            {
                Id = Guid.NewGuid(),
                KnowledgeBaseId = knowledgeBaseId,
                Type = input.Type,
                Content = textChunks[i],
                SourceId = input.SourceId,
                ChunkIndex = i,
                CreatedAt = DateTime.UtcNow
            });
        }

        // 批量生成向量
        var texts = chunks.Select(c => c.Content).ToList();
        var vectors = await _embeddingService.EmbedBatchAsync(texts);

        // 向量写入 SK VectorStore
        var pairs = chunks.Zip(vectors, (chunk, vector) => (chunk, vector)).ToList();
        await _retrievalService.AddChunksAsync(pairs);

        // 结构化数据写入 SQL Server
        await _chunkRepository.AddRangeAsync(chunks);
        await _kbRepository.UpdateChunkCountAsync(knowledgeBaseId);

        _logger.LogInformation("文档上传成功，分 {Count} 块存入知识库 {KbId}", chunks.Count, knowledgeBaseId);
        return chunks.Select(MapToChunkDto).ToList();
    }

    // ========== 检索 ==========

    public async Task<List<KnowledgeChunkDto>> RetrieveAsync(RetrieveQueryDto input)
    {
        // 默认使用第一个知识库（兼容旧接口）
        var kbs = await _kbRepository.GetAllAsync();
        var kbId = kbs.FirstOrDefault()?.Id ?? Guid.Empty;

        var results = await _retrievalService.RetrieveAsync(input.Query, kbId, input.TopK);
        return results.Select(MapToChunkDto).ToList();
    }

    // ========== 辅助方法 ==========

    private static KnowledgeBaseDto MapToDto(KnowledgeBase entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        ChunkCount = entity.ChunkCount,
        IsEnabled = entity.IsEnabled,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static KnowledgeChunkDto MapToChunkDto(KnowledgeChunk entity) => new()
    {
        Id = entity.Id,
        KnowledgeBaseId = entity.KnowledgeBaseId,
        Type = entity.Type,
        Content = entity.Content,
        Metadata = entity.Metadata,
        RelationId = entity.RelationId,
        SenderId = entity.SenderId,
        ChunkIndex = entity.ChunkIndex,
        SourceId = entity.SourceId,
        CreatedAt = entity.CreatedAt
    };
}
