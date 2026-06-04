using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using WeiDin.Core.Entities;
using WeiDin.Core.Interfaces;
using WeiDin.Core.Models;
using WeiDin.Infrastructure.Repositories;

namespace WeiDin.Application.Services.Knowledge;

/// <summary>
/// RAG 检索服务 — 通过 SK 向量存储进行检索
/// </summary>
public class RetrievalService : IRetrievalService
{
    private readonly VectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly KnowledgeChunkRepository _chunkRepository;
    private readonly ILogger<RetrievalService> _logger;

    public RetrievalService(
        VectorStore vectorStore,
        IEmbeddingService embeddingService,
        KnowledgeChunkRepository chunkRepository,
        ILogger<RetrievalService> logger)
    {
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _chunkRepository = chunkRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<KnowledgeChunk>> RetrieveAsync(string query, Guid knowledgeBaseId, int topK = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = GetCollection(knowledgeBaseId);

            if (!await collection.CollectionExistsAsync(cancellationToken))
            {
                return new List<KnowledgeChunk>();
            }

            // 1. 将查询文本转换为向量
            var queryVector = await _embeddingService.EmbedAsync(query, cancellationToken);

            // 2. 向量搜索
            var chunks = new List<KnowledgeChunk>();
            await foreach (var result in collection.SearchAsync(queryVector, topK, cancellationToken: cancellationToken))
            {
                var record = result.Record;
                chunks.Add(new KnowledgeChunk
                {
                    Id = Guid.Parse(record.Id),
                    KnowledgeBaseId = knowledgeBaseId,
                    Content = record.Content,
                    Metadata = $"{{\"distance\":{result.Score ?? 0}}}"
                });
            }

            _logger.LogDebug("检索到 {Count} 个知识块，知识库: {KbId}", chunks.Count, knowledgeBaseId);
            return chunks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检索失败，知识库: {KbId}", knowledgeBaseId);
            return new List<KnowledgeChunk>();
        }
    }

    /// <inheritdoc />
    public async Task AddChunkAsync(KnowledgeChunk chunk, float[] vector, CancellationToken cancellationToken = default)
    {
        await AddChunksAsync(new[] { (chunk, vector) }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddChunksAsync(IEnumerable<(KnowledgeChunk chunk, float[] vector)> chunks, CancellationToken cancellationToken = default)
    {
        var chunksList = chunks.ToList();
        if (chunksList.Count == 0) return;

        try
        {
            var knowledgeBaseId = chunksList.First().chunk.KnowledgeBaseId;
            var collection = GetCollection(knowledgeBaseId);
            await collection.EnsureCollectionExistsAsync(cancellationToken);

            var records = chunksList.Select(c => new ChunkVectorRecord
            {
                Id = c.chunk.Id.ToString(),
                KnowledgeBaseId = c.chunk.KnowledgeBaseId.ToString(),
                KnowledgeId = c.chunk.KnowledgeId.ToString(),
                Content = c.chunk.Content,
                Embedding = new ReadOnlyMemory<float>(c.vector)
            }).ToList();

            await collection.UpsertAsync(records, cancellationToken);

            _logger.LogInformation("添加 {Count} 个向量到 VectorStore，知识库: {KbId}", records.Count, knowledgeBaseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加向量失败");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteChunkAsync(Guid chunkId, Guid knowledgeBaseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = GetCollection(knowledgeBaseId);
            await collection.DeleteAsync(chunkId.ToString(), cancellationToken);
            _logger.LogInformation("删除向量 {ChunkId}，知识库: {KbId}", chunkId, knowledgeBaseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除向量失败: {ChunkId}", chunkId);
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(Guid knowledgeBaseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = GetCollection(knowledgeBaseId);
            if (await collection.CollectionExistsAsync(cancellationToken))
            {
                await collection.EnsureCollectionDeletedAsync(cancellationToken);
            }

            _logger.LogInformation("清空知识库向量: {KbId}", knowledgeBaseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空向量失败: {KbId}", knowledgeBaseId);
        }
    }

    private VectorStoreCollection<string, ChunkVectorRecord> GetCollection(Guid knowledgeBaseId)
    {
        return _vectorStore.GetCollection<string, ChunkVectorRecord>($"kb_{knowledgeBaseId}");
    }
}
