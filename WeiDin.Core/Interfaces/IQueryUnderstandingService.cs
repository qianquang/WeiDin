namespace WeiDin.Core.Interfaces;

/// <summary>
/// 查询理解服务 — 在检索前对用户问题进行改写和意图分类
/// 一次 LLM 调用同时完成两件事：
/// 1. 结合对话历史将问题改写为更独立、更易检索的查询
/// 2. 判断用户意图（需要知识库检索 / 直接对话）
/// </summary>
public interface IQueryUnderstandingService
{
    Task<QueryUnderstandingResult> AnalyzeAsync(
        string question,
        List<string>? conversationHistory = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 查询理解结果
/// </summary>
public class QueryUnderstandingResult
{
    /// <summary>
    /// 改写后的查询词，用于向量检索（比原始问题更独立、更精确）
    /// </summary>
    public string RewrittenQuery { get; set; } = string.Empty;

    /// <summary>
    /// 意图分类
    /// </summary>
    public QueryIntent Intent { get; set; } = QueryIntent.KnowledgeSearch;
}

/// <summary>
/// 用户意图分类
/// </summary>
public enum QueryIntent
{
    /// <summary>
    /// 需要检索知识库
    /// </summary>
    KnowledgeSearch = 0,

    /// <summary>
    /// 直接对话，不需要检索（如闲聊、问候）
    /// </summary>
    DirectChat = 1
}
