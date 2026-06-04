namespace WeiDin.Core.Interfaces;

/// <summary>
/// Rerank 精排服务接口 - 对粗排结果进行交叉编码打分，提升检索精度
/// </summary>
public interface IRerankService
{
    /// <summary>
    /// 对候选文档按与查询的相关性打分
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="documents">候选文档列表</param>
    /// <returns>每个文档的相关性得分，与 documents 等长、等序</returns>
    Task<float[]> ScoreAsync(string query, List<string> documents, CancellationToken cancellationToken = default);
}
