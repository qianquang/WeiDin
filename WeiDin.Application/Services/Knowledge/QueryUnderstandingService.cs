using System.Text.Json;
using Microsoft.Extensions.Logging;
using WeiDin.Core.Interfaces;

namespace WeiDin.Application.Services.Knowledge;

/// <summary>
/// 查询理解服务实现 — 通过单次 LLM 调用完成查询改写和意图分类
/// </summary>
public class QueryUnderstandingService : IQueryUnderstandingService
{
    private readonly ILlmService _llmService;
    private readonly ILogger<QueryUnderstandingService> _logger;

    // 系统提示词，要求 LLM 输出结构化 JSON
    private const string SystemPrompt = "你是查询理解助手。你的任务是分析用户问题，完成两件事：\n" +
        "\n" +
        "1. 查询改写：结合对话历史，将用户问题改写为一个独立、完整、适合向量检索的查询。去除指代不明的词语（如那个、它），补充必要的上下文。\n" +
        "2. 意图分类：判断用户是想搜索知识库，还是只是闲聊/问候。\n" +
        "\n" +
        "请严格按以下 JSON 格式输出，不要输出其他内容：\n" +
        "{\n" +
        "  \"rewrite_query\": \"改写后的查询\",\n" +
        "  \"intent\": \"KnowledgeSearch\" 或 \"DirectChat\"\n" +
        "}\n" +
        "\n" +
        "分类规则：\n" +
        "- KnowledgeSearch：用户在询问具体知识、事实、定义、操作步骤等需要参考资料的问题\n" +
        "- DirectChat：用户在闲聊、问候、表达情感、或问题不需要知识库就能回答（如你好、谢谢、今天天气怎么样）";

    public QueryUnderstandingService(
        ILlmService llmService,
        ILogger<QueryUnderstandingService> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<QueryUnderstandingResult> AnalyzeAsync(
        string question,
        List<string>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        // 没有 LLM 服务时直接返回默认值（降级：当知识库检索处理）
        try
        {
            // 构建用户消息：包含对话历史和当前问题
            var userMessage = BuildUserMessage(question, conversationHistory);

            // 调用 LLM
            var rawResponse = await _llmService.ChatAsync(SystemPrompt, userMessage, cancellationToken);

            // 解析结构化输出
            var result = ParseOutput(rawResponse, question);

            _logger.LogInformation(
                "查询理解完成: 原始=\"{Original}\" → 改写=\"{Rewritten}\", 意图={Intent}",
                Truncate(question, 50), Truncate(result.RewrittenQuery, 50), result.Intent);

            return result;
        }
        catch (Exception ex)
        {
            // 降级：查询理解失败时，使用原始问题，当知识库检索处理
            _logger.LogWarning(ex, "查询理解失败，降级使用原始问题");
            return new QueryUnderstandingResult
            {
                RewrittenQuery = question,
                Intent = QueryIntent.KnowledgeSearch
            };
        }
    }

    /// <summary>
    /// 构建包含对话历史的用户消息
    /// </summary>
    private static string BuildUserMessage(string question, List<string>? conversationHistory)
    {
        if (conversationHistory == null || conversationHistory.Count == 0)
        {
            return $"当前问题：{question}";
        }

        // 取最近几轮对话
        var recentHistory = conversationHistory.TakeLast(6).ToList();
        var historyText = string.Join("\n", recentHistory.Select((msg, i) => $"{i + 1}. {msg}"));

        return $"对话历史：\n{historyText}\n\n当前问题：{question}";
    }

    /// <summary>
    /// 解析 LLM 输出的 JSON
    /// </summary>
    private QueryUnderstandingResult ParseOutput(string rawResponse, string originalQuestion)
    {
        try
        {
            // 尝试提取 JSON（可能被 markdown 代码块包裹）
            var json = ExtractJson(rawResponse);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 提取改写查询（兼容多种字段名）
            var rewrittenQuery = FindStringField(root, "rewrite_query", "rewritten_query", "query")
                ?? originalQuestion;

            // 提取意图
            var intentStr = FindStringField(root, "intent", "type") ?? "KnowledgeSearch";
            var intent = intentStr.ToLower() switch
            {
                "directchat" or "direct_chat" or "chat" => QueryIntent.DirectChat,
                _ => QueryIntent.KnowledgeSearch
            };

            return new QueryUnderstandingResult
            {
                RewrittenQuery = rewrittenQuery,
                Intent = intent
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析查询理解输出失败，原始响应: {Response}", Truncate(rawResponse, 200));
            // 解析失败时，将整个响应作为改写查询
            return new QueryUnderstandingResult
            {
                RewrittenQuery = string.IsNullOrWhiteSpace(rawResponse) ? originalQuestion : rawResponse.Trim(),
                Intent = QueryIntent.KnowledgeSearch
            };
        }
    }

    /// <summary>
    /// 从 LLM 响应中提取 JSON 字符串（处理 markdown 代码块包裹的情况）
    /// </summary>
    private static string ExtractJson(string text)
    {
        // 去除 markdown 代码块标记
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```json"))
            trimmed = trimmed.Substring(7);
        else if (trimmed.StartsWith("```"))
            trimmed = trimmed.Substring(3);

        if (trimmed.EndsWith("```"))
            trimmed = trimmed.Substring(0, trimmed.Length - 3);

        return trimmed.Trim();
    }

    /// <summary>
    /// 从 JSON 对象中按优先级查找字符串字段
    /// </summary>
    private static string? FindStringField(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
    }

    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }
}
