using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text;
using WeiDin.Core.Entities;
using WeiDin.Core.Interfaces;
using WeiDin.Infrastructure.Repositories;

namespace WeiDin.Application.Services;

/// <summary>
/// RAG 检索服务
/// - 向量存储在数据库中 (byte[])
/// - 检索时从数据库加载向量到内存索引进行计算
/// - 省掉JSON文件步骤
/// </summary>
public class RetrievalService : IRetrievalService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly KnowledgeChunkRepository _chunkRepository;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RetrievalService> _logger;

    // 内存索引: 从数据库加载向量到内存
    private readonly Dictionary<Guid, float[]> _vectorIndex = new();
    private readonly int _dimension;
    private readonly Guid _defaultKnowledgeBaseId;
    private bool _initialized;
    private readonly object _lock = new();

    public RetrievalService(
        IEmbeddingService embeddingService,
        KnowledgeChunkRepository chunkRepository,
        KnowledgeBaseRepository knowledgeBaseRepository,
        IConfiguration configuration,
        ILogger<RetrievalService> logger)
    {
        _embeddingService = embeddingService;
        _chunkRepository = chunkRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _configuration = configuration;
        _logger = logger;

        _dimension = embeddingService.Dimension;

        // 从配置读取默认知识库ID
        var kbIdStr = configuration["VectorSearch:DefaultKnowledgeBaseId"];
        if (Guid.TryParse(kbIdStr, out var kbId))
        {
            _defaultKnowledgeBaseId = kbId;
        }
        else
        {
            _defaultKnowledgeBaseId = Guid.NewGuid();
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        lock (_lock)
        {
            if (_initialized)
                return;

            // 1. 确保默认知识库存在
            var knowledgeBase = _knowledgeBaseRepository.GetByIdAsync(_defaultKnowledgeBaseId).Result;
            if (knowledgeBase == null)
            {
                knowledgeBase = new KnowledgeBase
                {
                    Id = _defaultKnowledgeBaseId,
                    Name = "Default Knowledge Base",
                    Description = "默认知识库，用于存储聊天记录等"
                };
                _knowledgeBaseRepository.AddAsync(knowledgeBase).Wait();
                _logger.LogInformation("Created default knowledge base: {KnowledgeBaseId}", _defaultKnowledgeBaseId);
            }

            // 2. 从数据库加载向量到内存索引
            LoadVectorsFromDatabaseAsync().Wait();

            _initialized = true;
        }
    }

    /// <summary>
    /// 从数据库加载向量到内存索引
    /// </summary>
    private async Task LoadVectorsFromDatabaseAsync()
    {
        try
        {
            var chunks = await _chunkRepository.GetByKnowledgeBaseIdAsync(_defaultKnowledgeBaseId);

            foreach (var chunk in chunks)
            {
                if (chunk.Vector != null && chunk.Vector.Length > 0)
                {
                    // 从数据库读取向量 (byte[] → float[])
                    var vector = BytesToFloats(chunk.Vector);
                    _vectorIndex[chunk.Id] = vector;
                }
            }

            _logger.LogInformation("Loaded {Count} vectors from database to memory", _vectorIndex.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading vectors from database");
        }
    }

    /// <inheritdoc />
    public async Task<List<KnowledgeChunk>> RetrieveAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        try
        {
            // 1. 将查询文本转换为向量
            var queryVector = await _embeddingService.EmbedAsync(query, cancellationToken);

            // 2. 在内存索引中搜索 (余弦相似度)
            var results = SearchInMemory(queryVector, topK);

            if (results.Count == 0)
            {
                return new List<KnowledgeChunk>();
            }

            // 3. 根据ID从数据库加载完整知识块
            var chunkIds = results.Select(r => r.chunkId).ToList();
            var chunks = await _chunkRepository.GetByIdsAsync(chunkIds);

            // 4. 按相似度排序
            var chunkDict = chunks.ToDictionary(c => c.Id);
            var orderedResults = new List<KnowledgeChunk>();

            foreach (var (chunkId, score) in results)
            {
                if (chunkDict.TryGetValue(chunkId, out var chunk))
                {
                    chunk.Metadata = $"{{\"distance\":{score}}}";
                    orderedResults.Add(chunk);
                }
            }

            _logger.LogDebug("Retrieved {Count} chunks for query: {Query}", orderedResults.Count, query.Substring(0, Math.Min(50, query.Length)));
            return orderedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chunks for query: {Query}", query);
            return new List<KnowledgeChunk>();
        }
    }

    /// <summary>
    /// 内存中搜索向量 (余弦相似度)
    /// </summary>
    private List<(Guid chunkId, float score)> SearchInMemory(float[] queryVector, int topK)
    {
        var results = new List<(Guid, float)>();

        // 归一化查询向量
        var normalizedQuery = NormalizeVector(queryVector);

        foreach (var (chunkId, vector) in _vectorIndex)
        {
            var similarity = CosineSimilarity(normalizedQuery, vector);
            results.Add((chunkId, similarity));
        }

        // 排序并返回TopK
        return results
            .OrderByDescending(r => r.Item2)
            .Take(topK)
            .ToList();
    }

    /// <inheritdoc />
    public async Task AddChunkAsync(KnowledgeChunk chunk, CancellationToken cancellationToken = default)
    {
        await AddChunksAsync(new[] { chunk }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddChunksAsync(IEnumerable<KnowledgeChunk> chunks, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        var chunksList = chunks.ToList();
        if (chunksList.Count == 0)
            return;

        try
        {
            // 1. 确保知识块有ID
            foreach (var chunk in chunksList)
            {
                if (chunk.KnowledgeBaseId == Guid.Empty)
                    chunk.KnowledgeBaseId = _defaultKnowledgeBaseId;
                if (chunk.Id == Guid.Empty)
                    chunk.Id = Guid.NewGuid();
            }

            // 2. 批量生成向量
            var texts = chunksList.Select(c => c.Content).ToList();
            var vectors = await _embeddingService.EmbedBatchAsync(texts, cancellationToken);

            // 3. 存数据库 (原文 + 向量)，并加载到内存索引
            for (int i = 0; i < chunksList.Count; i++)
            {
                var chunk = chunksList[i];
                var vector = vectors[i];

                // 转换向量为字节数组存数据库
                chunk.Vector = FloatsToBytes(vector);

                // 加载到内存索引
                _vectorIndex[chunk.Id] = vector;
            }

            // 4. 批量添加到数据库
            await _chunkRepository.AddRangeAsync(chunksList);

            // 5. 更新知识库块数量
            await _knowledgeBaseRepository.UpdateChunkCountAsync(_defaultKnowledgeBaseId);

            _logger.LogInformation("Added {Count} chunks to database and memory index", chunksList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding chunks");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteChunkAsync(string chunkId, CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(chunkId, out var guid))
        {
            // 从内存索引移除
            _vectorIndex.Remove(guid);

            // 从数据库删除
            await _chunkRepository.DeleteByIdAsync(guid);

            _logger.LogInformation("Deleted chunk {ChunkId}", chunkId);
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        // 1. 清空内存索引
        _vectorIndex.Clear();

        // 2. 删除数据库中的知识块
        var chunks = await _chunkRepository.GetByKnowledgeBaseIdAsync(_defaultKnowledgeBaseId);
        foreach (var chunk in chunks)
        {
            await _chunkRepository.DeleteAsync(chunk);
        }

        _initialized = false;

        _logger.LogInformation("Cleared knowledge base");
    }

    #region 辅助方法

    /// <summary>
    /// float[] 转 byte[] (用于数据库存储)
    /// </summary>
    private static byte[] FloatsToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * 4];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// byte[] 转 float[] (从数据库读取)
    /// </summary>
    private static float[] BytesToFloats(byte[] bytes)
    {
        var floats = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    /// <summary>
    /// 向量归一化
    /// </summary>
    private static float[] NormalizeVector(float[] vector)
    {
        var norm = 0f;
        foreach (var v in vector)
            norm += v * v;
        norm = MathF.Sqrt(norm);

        if (norm > 0)
        {
            var result = new float[vector.Length];
            for (int i = 0; i < vector.Length; i++)
                result[i] = vector[i] / norm;
            return result;
        }

        return vector;
    }

    /// <summary>
    /// 余弦相似度计算
    /// </summary>
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    #endregion
}
