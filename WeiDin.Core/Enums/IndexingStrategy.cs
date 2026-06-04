namespace WeiDin.Core.Enums;

/// <summary>
/// 索引策略枚举 — 决定知识库使用哪种检索方式
/// </summary>
public enum IndexingStrategy
{
    /// <summary>
    /// 向量检索（默认）
    /// </summary>
    Vector = 0,

    /// <summary>
    /// 关键词检索
    /// </summary>
    Keyword = 1,

    /// <summary>
    /// Wiki 式检索
    /// </summary>
    Wiki = 2,

    /// <summary>
    /// 图谱检索
    /// </summary>
    Graph = 3
}
