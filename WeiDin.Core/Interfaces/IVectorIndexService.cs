namespace WeiDin.Core.Interfaces;

/// <summary>
/// 向量索引服务接口
/// </summary>
public interface IVectorIndexService
{
    /// <summary>
    /// 创建向量索引
    /// </summary>
    /// <param name="indexName">索引名称</param>
    /// <param name="dimension">向量维度</param>
    Task CreateIndexAsync(string indexName, int dimension);

    /// <summary>
    /// 添加向量到索引
    /// </summary>
    /// <param name="indexName">索引名称</param>
    /// <param name="id">向量ID</param>
    /// <param name="vector">向量数据</param>
    Task AddVectorAsync(string indexName, string id, float[] vector);

    /// <summary>
    /// 批量添加向量到索引
    /// </summary>
    /// <param name="indexName">索引名称</param>
    /// <param name="vectors">向量字典 (id -> vector)</param>
    Task AddVectorsAsync(string indexName, Dictionary<string, float[]> vectors);

    /// <summary>
    /// 搜索最近邻向量
    /// </summary>
    /// <param name="indexName">索引名称</param>
    /// <param name="queryVector">查询向量</param>
    /// <param name="topK">返回结果数量</param>
    /// <returns>搜索结果 (id, 距离)</returns>
    Task<List<(string Id, float Distance)>> SearchAsync(string indexName, float[] queryVector, int topK = 5);

    /// <summary>
    /// 删除向量索引
    /// </summary>
    /// <param name="indexName">索引名称</param>
    Task DeleteIndexAsync(string indexName);

    /// <summary>
    /// 检查索引是否存在
    /// </summary>
    /// <param name="indexName">索引名称</param>
    /// <returns>是否存在</returns>
    bool IndexExists(string indexName);

    /// <summary>
    /// 保存索引到持久化存储
    /// </summary>
    /// <param name="indexName">索引名称</param>
    Task SaveIndexAsync(string indexName);

    /// <summary>
    /// 从持久化存储加载索引
    /// </summary>
    /// <param name="indexName">索引名称</param>
    Task LoadIndexAsync(string indexName);
}
