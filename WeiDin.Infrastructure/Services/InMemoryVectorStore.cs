using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;

namespace WeiDin.Infrastructure.Services;

/// <summary>
/// 简单的内存向量存储实现 — 基于 VectorStore 抽象类
/// 使用余弦相似度进行暴力搜索，适合开发和小规模数据
/// 生产环境可替换为 Qdrant/Redis 等真正的向量数据库
/// </summary>
public class InMemoryVectorStore : VectorStore
{
    private readonly ConcurrentDictionary<string, object> _collections = new();

    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
        string collectionName,
        VectorStoreCollectionDefinition? vectorStoreRecordDefinition = null)
    {
        var collection = _collections.GetOrAdd(collectionName,
            _ => new InMemoryVectorStoreCollection<TKey, TRecord>(collectionName));
        return (VectorStoreCollection<TKey, TRecord>)collection;
    }

    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(
        string collectionName,
        VectorStoreCollectionDefinition? vectorStoreRecordDefinition = null)
    {
        throw new NotSupportedException("Dynamic collections are not supported by InMemoryVectorStore");
    }

    public override async IAsyncEnumerable<string> ListCollectionNamesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var name in _collections.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return name;
        }
    }

    public override Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_collections.ContainsKey(collectionName));
    }

    public override Task EnsureCollectionDeletedAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        _collections.TryRemove(collectionName, out _);
        return Task.CompletedTask;
    }

    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }
}

/// <summary>
/// 内存向量记录集合 — 支持 POCO 类型的泛型实现
/// </summary>
public class InMemoryVectorStoreCollection<TKey, TRecord> :
    VectorStoreCollection<TKey, TRecord>
    where TKey : notnull
    where TRecord : class
{
    private readonly string _collectionName;
    private readonly ConcurrentDictionary<string, TRecord> _records = new();

    private readonly PropertyInfo _keyProperty;
    private readonly PropertyInfo? _vectorProperty;

    public override string Name => _collectionName;

    public InMemoryVectorStoreCollection(string collectionName)
    {
        _collectionName = collectionName;

        var type = typeof(TRecord);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (prop.GetCustomAttribute<VectorStoreKeyAttribute>() != null)
            {
                _keyProperty = prop;
            }
            else if (prop.GetCustomAttribute<VectorStoreVectorAttribute>() != null)
            {
                _vectorProperty = prop;
            }
        }

        _keyProperty ??= properties.FirstOrDefault(p => p.Name == "Id")
            ?? throw new InvalidOperationException($"Type {type.Name} must have a property with [VectorStoreKey] or named 'Id'");
    }

    public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public override Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public override Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        _records.Clear();
        return Task.CompletedTask;
    }

    public override Task<TRecord?> GetAsync(TKey key, RecordRetrievalOptions? options = null, CancellationToken cancellationToken = default)
    {
        _records.TryGetValue(key.ToString()!, out var record);
        return Task.FromResult(record);
    }

    public override async IAsyncEnumerable<TRecord> GetAsync(IEnumerable<TKey> keys, RecordRetrievalOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_records.TryGetValue(key.ToString()!, out var record))
            {
                yield return record;
            }
        }
    }

    public override async IAsyncEnumerable<TRecord> GetAsync(Expression<Func<TRecord, bool>> filter, int top, FilteredRecordRetrievalOptions<TRecord>? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var compiled = filter.Compile();
        var results = _records.Values.Where(compiled).Take(top);
        foreach (var record in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
        }
    }

    public override Task<TKey> UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        var key = GetKey(record);
        _records[key.ToString()!] = record;
        return Task.FromResult(key);
    }

    public override Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = GetKey(record);
            _records[key.ToString()!] = record;
        }
        return Task.CompletedTask;
    }

    public override Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        _records.TryRemove(key.ToString()!, out _);
        return Task.CompletedTask;
    }

    public override Task DeleteAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            _records.TryRemove(key.ToString()!, out _);
        }
        return Task.CompletedTask;
    }

    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TVector>(
        TVector vector,
        int top,
        VectorSearchOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_vectorProperty == null) yield break;

        ReadOnlyMemory<float> queryVector;
        if (vector is ReadOnlyMemory<float> rom)
            queryVector = rom;
        else if (vector is float[] arr)
            queryVector = new ReadOnlyMemory<float>(arr);
        else
            yield break;

        var results = new List<(TRecord Record, double Score)>();

        foreach (var (_, record) in _records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var storedVector = _vectorProperty.GetValue(record) as ReadOnlyMemory<float>?;
            if (storedVector.HasValue)
            {
                var score = CosineSimilarity(queryVector.Span, storedVector.Value.Span);
                results.Add((record, score));
            }
        }

        var sorted = results.OrderByDescending(r => r.Score).Take(top);

        foreach (var (record, score) in sorted)
        {
            yield return new VectorSearchResult<TRecord>(record, score);
        }
    }

    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    private TKey GetKey(TRecord record)
    {
        var value = _keyProperty.GetValue(record);
        if (value == null)
            throw new InvalidOperationException("Key property cannot be null");

        if (value is TKey typedKey)
            return typedKey;

        return (TKey)Convert.ChangeType(value, typeof(TKey));
    }

    private static double CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length) return 0;

        double dotProduct = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator > 0 ? dotProduct / denominator : 0;
    }
}
