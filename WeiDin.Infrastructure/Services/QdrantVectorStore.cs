using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace WeiDin.Infrastructure.Services;

/// <summary>
/// Qdrant 向量存储实现 — 基于 SK VectorStore 抽象类
/// 使用 Qdrant.Client gRPC 连接 Qdrant 服务端
/// </summary>
public class QdrantVectorStore : VectorStore
{
    private readonly QdrantClient _qdrantClient;
    private readonly ConcurrentDictionary<string, object> _collections = new();

    public QdrantVectorStore(QdrantClient qdrantClient)
    {
        _qdrantClient = qdrantClient;
    }

    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
        string collectionName,
        VectorStoreCollectionDefinition? vectorStoreRecordDefinition = null)
    {
        var collection = _collections.GetOrAdd(collectionName,
            _ => new QdrantVectorStoreCollection<TKey, TRecord>(_qdrantClient, collectionName));
        return (VectorStoreCollection<TKey, TRecord>)collection;
    }

    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(
        string collectionName,
        VectorStoreCollectionDefinition? vectorStoreRecordDefinition = null)
    {
        throw new NotSupportedException("Dynamic collections are not supported by QdrantVectorStore");
    }

    public override async IAsyncEnumerable<string> ListCollectionNamesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var collections = await _qdrantClient.ListCollectionsAsync(cancellationToken);
        foreach (var name in collections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return name;
        }
    }

    public override async Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        var collections = await _qdrantClient.ListCollectionsAsync(cancellationToken);
        return collections.Contains(collectionName);
    }

    public override async Task EnsureCollectionDeletedAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        await _qdrantClient.DeleteCollectionAsync(collectionName, cancellationToken: cancellationToken);
        _collections.TryRemove(collectionName, out _);
    }

    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(QdrantClient))
            return _qdrantClient;
        return null;
    }
}

/// <summary>
/// Qdrant 向量记录集合 — 支持 POCO 类型的泛型实现
/// </summary>
public class QdrantVectorStoreCollection<TKey, TRecord> :
    VectorStoreCollection<TKey, TRecord>
    where TKey : notnull
    where TRecord : class
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;

    private readonly PropertyInfo _keyProperty;
    private readonly PropertyInfo? _vectorProperty;
    private readonly List<PropertyInfo> _dataProperties = new();

    // 用于反射缓存
    private static readonly Type StringType = typeof(string);
    private static readonly Type IntType = typeof(int);
    private static readonly Type LongType = typeof(long);
    private static readonly Type GuidType = typeof(Guid);

    public override string Name => _collectionName;

    public QdrantVectorStoreCollection(QdrantClient client, string collectionName)
    {
        _client = client;
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
            else if (prop.GetCustomAttribute<VectorStoreDataAttribute>() != null)
            {
                _dataProperties.Add(prop);
            }
        }

        _keyProperty ??= properties.FirstOrDefault(p => p.Name == "Id")
            ?? throw new InvalidOperationException($"Type {type.Name} must have a property with [VectorStoreKey] or named 'Id'");
    }

    public override async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        var collections = await _client.ListCollectionsAsync(cancellationToken);
        return collections.Contains(_collectionName);
    }

    public override async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        var exists = await CollectionExistsAsync(cancellationToken);
        if (exists) return;

        // 从属性标注中获取向量维度
        var vectorAttr = _vectorProperty?.GetCustomAttribute<VectorStoreVectorAttribute>();
        var vectorSize = vectorAttr?.Dimensions ?? 512;

        await _client.CreateCollectionAsync(_collectionName, new VectorParams
        {
            Size = (ulong)vectorSize,
            Distance = Distance.Cosine
        }, cancellationToken: cancellationToken);

        // 为 indexed data 字段创建 payload 索引
        foreach (var prop in _dataProperties)
        {
            var dataAttr = prop.GetCustomAttribute<VectorStoreDataAttribute>();
            if (dataAttr is { IsIndexed: true })
            {
                await _client.CreatePayloadIndexAsync(_collectionName, prop.Name,
                    cancellationToken: cancellationToken);
            }
        }
    }

    public override async Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        await _client.DeleteCollectionAsync(_collectionName, cancellationToken: cancellationToken);
    }

    public override async Task<TRecord?> GetAsync(TKey key, RecordRetrievalOptions? options = null, CancellationToken cancellationToken = default)
    {
        var pointId = ConvertToPointId(key);
        var points = await _client.RetrieveAsync(_collectionName, pointId, cancellationToken: cancellationToken);
        var point = points.FirstOrDefault();
        return point != null ? MapToPointRecord(point) : null;
    }

    public override async IAsyncEnumerable<TRecord> GetAsync(IEnumerable<TKey> keys, RecordRetrievalOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pointIds = keys.Select(ConvertToPointId).ToList();
        var points = await _client.RetrieveAsync(_collectionName, pointIds, cancellationToken: cancellationToken);
        foreach (var point in points)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = MapToPointRecord(point);
            if (record != null) yield return record;
        }
    }

    public override async IAsyncEnumerable<TRecord> GetAsync(Expression<Func<TRecord, bool>> filter, int top, FilteredRecordRetrievalOptions<TRecord>? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Qdrant 的过滤通过 scroll 实现，这里用简单的方式：
        // 先获取所有点，再在内存中过滤（小规模数据可用）
        // 生产环境应使用 Qdrant 的 filter API
        var compiled = filter.Compile();
        var results = new List<TRecord>();

        var points = await _client.ScrollAsync(_collectionName, limit: (uint)(top * 10),
            cancellationToken: cancellationToken);

        foreach (var point in points.Result)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = MapToPointRecord(point);
            if (record != null && compiled(record))
            {
                results.Add(record);
                if (results.Count >= top) break;
            }
        }

        foreach (var record in results)
        {
            yield return record;
        }
    }

    public override async Task<TKey> UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        var point = MapToGrpcPoint(record);
        await _client.UpsertAsync(_collectionName, new[] { point }, cancellationToken: cancellationToken);
        return GetKey(record);
    }

    public override async Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        var points = records.Select(MapToGrpcPoint).ToList();
        if (points.Count == 0) return;
        await _client.UpsertAsync(_collectionName, points, cancellationToken: cancellationToken);
    }

    public override async Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        var pointId = ConvertToPointId(key);
        await _client.DeleteAsync(_collectionName, pointId, cancellationToken: cancellationToken);
    }

    public override async Task DeleteAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pointId = ConvertToPointId(key);
            await _client.DeleteAsync(_collectionName, pointId, cancellationToken: cancellationToken);
        }
    }

    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TVector>(
        TVector vector,
        int top,
        VectorSearchOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ReadOnlyMemory<float> queryVector;
        if (vector is ReadOnlyMemory<float> rom)
            queryVector = rom;
        else if (vector is float[] arr)
            queryVector = new ReadOnlyMemory<float>(arr);
        else
            yield break;

        var searchResult = await _client.SearchAsync(
            _collectionName,
            queryVector.ToArray(),
            limit: (uint)top,
            cancellationToken: cancellationToken);

        foreach (var scoredPoint in searchResult)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = MapToPointRecord(scoredPoint);
            if (record != null)
            {
                yield return new VectorSearchResult<TRecord>(record, scoredPoint.Score);
            }
        }
    }

    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    #region Mapping Helpers

    private TKey GetKey(TRecord record)
    {
        var value = _keyProperty.GetValue(record);
        if (value == null) throw new InvalidOperationException("Key property cannot be null");
        if (value is TKey typedKey) return typedKey;
        return (TKey)Convert.ChangeType(value, typeof(TKey));
    }

    /// <summary>
    /// 将 TKey 转换为 Qdrant PointId（支持 string / Guid / ulong / long / int）
    /// </summary>
    private PointId ConvertToPointId(TKey key)
    {
        if (key is string s)
        {
            if (Guid.TryParse(s, out var guid))
                return new PointId { Uuid = guid.ToString() };
            return new PointId { Num = (ulong)s.GetHashCode() };
        }
        if (key is Guid g)
            return new PointId { Uuid = g.ToString() };
        if (key is ulong ul)
            return new PointId { Num = ul };
        if (key is long l)
            return new PointId { Num = (ulong)l };
        if (key is int i)
            return new PointId { Num = (ulong)i };

        // fallback: 用 string 的 hash
        return new PointId { Num = (ulong)key.GetHashCode() };
    }

    /// <summary>
    /// 从 Qdrant PointId 还原为 TKey
    /// </summary>
    private TKey ConvertFromPointId(PointId pointId)
    {
        var keyType = typeof(TKey);

        if (keyType == StringType)
        {
            var str = pointId.Uuid ?? pointId.Num.ToString();
            return (TKey)(object)str;
        }
        if (keyType == GuidType)
            return (TKey)(object)Guid.Parse(pointId.Uuid);
        if (keyType == ULongType)
            return (TKey)(object)pointId.Num;
        if (keyType == LongType)
            return (TKey)(object)(long)pointId.Num;
        if (keyType == IntType)
            return (TKey)(object)(int)pointId.Num;

        return (TKey)Convert.ChangeType(pointId.Uuid ?? pointId.Num.ToString(), keyType);
    }

    private static readonly Type ULongType = typeof(ulong);

    /// <summary>
    /// 将 POCO 记录映射为 Qdrant gRPC PointStruct
    /// </summary>
    private PointStruct MapToGrpcPoint(TRecord record)
    {
        var pointId = ConvertToPointId(GetKey(record));
        var point = new PointStruct { Id = pointId };

        // 向量
        if (_vectorProperty != null)
        {
            var vectorValue = _vectorProperty.GetValue(record);
            ReadOnlyMemory<float>? embedding = vectorValue switch
            {
                ReadOnlyMemory<float> rom => rom,
                float[] arr => new ReadOnlyMemory<float>(arr),
                _ => null
            };

            if (embedding.HasValue)
            {
                var data = embedding.Value.ToArray();
                point.Vectors = new Vectors
                {
                    Vector = new Vector { Data = { data } }
                };
            }
        }

        // 数据字段
        foreach (var prop in _dataProperties)
        {
            var value = prop.GetValue(record);
            point.Payload[prop.Name] = ConvertToQdrantValue(value);
        }

        // Key 字段也存入 payload（便于检索时还原）
        point.Payload[_keyProperty.Name] = ConvertToQdrantValue(_keyProperty.GetValue(record));

        return point;
    }

    /// <summary>
    /// 将 Qdrant RetrievedPoint / ScoredPoint 映射回 POCO 记录
    /// </summary>
    private TRecord? MapToPointRecord(RetrievedPoint point)
    {
        var record = Activator.CreateInstance<TRecord>();

        // 设置 Key
        if (point.Payload.TryGetValue(_keyProperty.Name, out var keyVal))
        {
            _keyProperty.SetValue(record, ConvertFromQdrantValue(keyVal, _keyProperty.PropertyType));
        }

        // 设置数据字段
        foreach (var prop in _dataProperties)
        {
            if (point.Payload.TryGetValue(prop.Name, out var val))
            {
                prop.SetValue(record, ConvertFromQdrantValue(val, prop.PropertyType));
            }
        }

        return record;
    }

    /// <summary>
    /// 从 ScoredPoint 映射（搜索结果用）
    /// </summary>
    private TRecord? MapToPointRecord(ScoredPoint point)
    {
        var record = Activator.CreateInstance<TRecord>();

        if (point.Payload.TryGetValue(_keyProperty.Name, out var keyVal))
        {
            _keyProperty.SetValue(record, ConvertFromQdrantValue(keyVal, _keyProperty.PropertyType));
        }

        foreach (var prop in _dataProperties)
        {
            if (point.Payload.TryGetValue(prop.Name, out var val))
            {
                prop.SetValue(record, ConvertFromQdrantValue(val, prop.PropertyType));
            }
        }

        return record;
    }

    /// <summary>
    /// C# 值 → Qdrant gRPC Value
    /// </summary>
    private static Qdrant.Client.Grpc.Value ConvertToQdrantValue(object? value)
    {
        return value switch
        {
            null => new Qdrant.Client.Grpc.Value { NullValue = (Qdrant.Client.Grpc.NullValue)0 },
            string s => new Qdrant.Client.Grpc.Value { StringValue = s },
            int i => new Qdrant.Client.Grpc.Value { IntegerValue = i },
            long l => new Qdrant.Client.Grpc.Value { IntegerValue = l },
            float f => new Qdrant.Client.Grpc.Value { DoubleValue = f },
            double d => new Qdrant.Client.Grpc.Value { DoubleValue = d },
            bool b => new Qdrant.Client.Grpc.Value { BoolValue = b },
            Guid g => new Qdrant.Client.Grpc.Value { StringValue = g.ToString() },
            _ => new Qdrant.Client.Grpc.Value { StringValue = value.ToString() ?? "" }
        };
    }

    /// <summary>
    /// Qdrant gRPC Value → C# 值
    /// </summary>
    private static object? ConvertFromQdrantValue(Qdrant.Client.Grpc.Value value, Type targetType)
    {
        var raw = value.KindCase switch
        {
            Qdrant.Client.Grpc.Value.KindOneofCase.StringValue => (object)value.StringValue,
            Qdrant.Client.Grpc.Value.KindOneofCase.IntegerValue => (object)value.IntegerValue,
            Qdrant.Client.Grpc.Value.KindOneofCase.DoubleValue => (object)value.DoubleValue,
            Qdrant.Client.Grpc.Value.KindOneofCase.BoolValue => (object)value.BoolValue,
            _ => null
        };

        if (raw == null) return null;
        if (targetType.IsInstanceOfType(raw)) return raw;
        return Convert.ChangeType(raw, targetType);
    }

    #endregion
}
