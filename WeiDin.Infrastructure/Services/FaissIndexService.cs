using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WeiDin.Core.Interfaces;

namespace WeiDin.Infrastructure.Services;

/// <summary>
/// FAISS 向量索引服务 - 基于内存的实现
/// 使用余弦相似度进行向量检索
/// </summary>
public class FaissIndexService : IVectorIndexService
{
    private readonly Dictionary<string, List<(string Id, float[] Vector)>> _indices = new();
    private readonly Dictionary<string, int> _dimensions = new();
    private readonly string _indexPath;
    private readonly ILogger<FaissIndexService> _logger;
    private readonly object _lock = new();

    public FaissIndexService(IConfiguration configuration, ILogger<FaissIndexService> logger)
    {
        _logger = logger;
        _indexPath = configuration["VectorSearch:IndexPath"] ?? "Data/faiss";
        Directory.CreateDirectory(_indexPath);
    }

    /// <inheritdoc />
    public Task CreateIndexAsync(string indexName, int dimension)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (_indices.ContainsKey(indexName))
                {
                    _logger.LogWarning("Index {IndexName} already exists", indexName);
                    return;
                }

                _indices[indexName] = new List<(string, float[])>();
                _dimensions[indexName] = dimension;

                _logger.LogInformation("Created in-memory index {IndexName} with dimension {Dimension}", indexName, dimension);
            }
        });
    }

    /// <inheritdoc />
    public Task AddVectorAsync(string indexName, string id, float[] vector)
    {
        return AddVectorsAsync(indexName, new Dictionary<string, float[]> { { id, vector } });
    }

    /// <inheritdoc />
    public Task AddVectorsAsync(string indexName, Dictionary<string, float[]> vectors)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_indices.TryGetValue(indexName, out var index))
                {
                    throw new InvalidOperationException($"Index {indexName} does not exist");
                }

                foreach (var (id, vector) in vectors)
                {
                    // 归一化向量 (用于余弦相似度)
                    var normalized = NormalizeVector(vector);
                    index.Add((id, normalized));
                }

                _logger.LogInformation("Added {Count} vectors to index {IndexName}", vectors.Count, indexName);
            }
        });
    }

    /// <inheritdoc />
    public Task<List<(string Id, float Distance)>> SearchAsync(string indexName, float[] queryVector, int topK = 5)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_indices.TryGetValue(indexName, out var index))
                {
                    throw new InvalidOperationException($"Index {indexName} does not exist");
                }

                if (index.Count == 0)
                {
                    return new List<(string, float)>();
                }

                // 归一化查询向量
                var normalizedQuery = NormalizeVector(queryVector);

                // 计算余弦相似度
                var results = new List<(string, float)>();
                foreach (var (id, vector) in index)
                {
                    var similarity = CosineSimilarity(normalizedQuery, vector);
                    results.Add((id, similarity));
                }

                // 排序并取 Top K
                var sorted = results.OrderByDescending(r => r.Item2).Take(topK).ToList();

                return sorted;
            }
        });
    }

    /// <inheritdoc />
    public Task DeleteIndexAsync(string indexName)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (_indices.Remove(indexName))
                {
                    _dimensions.Remove(indexName);

                    // 删除持久化文件
                    var indexFile = Path.Combine(_indexPath, $"{indexName}.json");
                    if (File.Exists(indexFile))
                        File.Delete(indexFile);

                    _logger.LogInformation("Deleted index {IndexName}", indexName);
                }
            }
        });
    }

    /// <inheritdoc />
    public bool IndexExists(string indexName)
    {
        lock (_lock)
        {
            return _indices.ContainsKey(indexName);
        }
    }

    /// <inheritdoc />
    public Task SaveIndexAsync(string indexName)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                if (!_indices.TryGetValue(indexName, out var index))
                {
                    throw new InvalidOperationException($"Index {indexName} does not exist");
                }

                var data = new
                {
                    Dimension = _dimensions[indexName],
                    Vectors = index.Select(v => new { Id = v.Id, Vector = v.Vector }).ToList()
                };

                var indexFile = Path.Combine(_indexPath, $"{indexName}.json");
                var json = System.Text.Json.JsonSerializer.Serialize(data);
                File.WriteAllText(indexFile, json);

                _logger.LogInformation("Saved index {IndexName} to {Path}", indexName, _indexPath);
            }
        });
    }

    /// <inheritdoc />
    public Task LoadIndexAsync(string indexName)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                var indexFile = Path.Combine(_indexPath, $"{indexName}.json");

                if (!File.Exists(indexFile))
                {
                    _logger.LogWarning("Index file {IndexFile} not found", indexFile);
                    return;
                }

                var json = File.ReadAllText(indexFile);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var dimension = root.GetProperty("Dimension").GetInt32();
                var vectors = new List<(string, float[])>();

                if (root.TryGetProperty("Vectors", out var vectorsElement))
                {
                    foreach (var v in vectorsElement.EnumerateArray())
                    {
                        var id = v.GetProperty("Id").GetString() ?? "";
                        var vectorArray = v.GetProperty("Vector").EnumerateArray().Select(x => x.GetSingle()).ToArray();
                        vectors.Add((id, vectorArray));
                    }
                }

                _indices[indexName] = vectors;
                _dimensions[indexName] = dimension;

                _logger.LogInformation("Loaded index {IndexName} with {Count} vectors from {Path}",
                    indexName, vectors.Count, _indexPath);
            }
        });
    }

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

        var result = dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
        return result;
    }
}
