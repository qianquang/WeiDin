using Microsoft.Extensions.Logging;
using WeiDin.Core.Interfaces;

namespace WeiDin.Application.Services.Knowledge;

/// <summary>
/// RAG Agent 引擎 — 实现"输入 → 查询理解 → 向量检索 → LLM 生成 → 输出"管道
///
/// 当前实现是单轮 RAG 问答，不具备真正的 Agent 能力（无循环推理、无工具调用、无状态管理）。
/// 命名为 Agent 是为了后续扩展预留，目前等价于 ChatCompletionService 中的 RAG 问答功能。
/// </summary>
public class RagAgentEngine : IAgentEngine
{
    private readonly IRetrievalService _retrievalService;          // 向量检索服务
    private readonly ILlmService _llmService;                      // LLM 调用服务
    private readonly IQueryUnderstandingService _queryUnderstanding; // 查询理解服务
    private readonly IRerankService _rerankService;                // Rerank 精排服务
    private readonly ILogger<RagAgentEngine> _logger;

    // RAG 系统提示词模板
    private const string RagSystemPrompt = @"你是一个知识问答助手。请根据以下知识库内容回答用户的问题。

要求：
- 仅基于提供的知识内容回答，不要编造信息
- 如果知识库中没有相关信息，请如实说明
- 回答要简洁、准确、有条理

知识库内容：
{knowledgeContext}";

    // 直接对话系统提示词（不需要检索时使用）
    private const string DirectChatSystemPrompt = @"你是一个友好的助手。请直接回答用户的问题，不需要引用知识库。回答要简洁、有帮助。";

    public RagAgentEngine(
        IRetrievalService retrievalService,
        ILlmService llmService,
        IQueryUnderstandingService queryUnderstanding,
        IRerankService rerankService,
        ILogger<RagAgentEngine> logger)
    {
        _retrievalService = retrievalService;
        _llmService = llmService;
        _queryUnderstanding = queryUnderstanding;
        _rerankService = rerankService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AgentResponse> AskAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            throw new ArgumentException("问题不能为空", nameof(request));

        _logger.LogInformation("Agent 收到问题: {Question}", Truncate(request.Question, 100));

        // ── 第一步：查询理解（改写 + 意图分类）──
        var understanding = await _queryUnderstanding.AnalyzeAsync(
            request.Question,
            request.ConversationHistory,
            cancellationToken);

        _logger.LogInformation("查询理解: 改写=\"{Rewritten}\", 意图={Intent}",
            Truncate(understanding.RewrittenQuery, 80), understanding.Intent);

        // ── 第二步：根据意图分流 ──
        if (understanding.Intent == QueryIntent.DirectChat)
        {
            // 意图：直接对话 → 不检索，直接问 LLM
            var directAnswer = await _llmService.ChatAsync(
                DirectChatSystemPrompt, request.Question, cancellationToken);

            return new AgentResponse
            {
                Answer = directAnswer,
                Sources = new List<string>(),
                ChunksUsed = 0
            };
        }

        // ── 第三步：向量粗排 + Rerank 精排 ──
        var retrievalTopK = request.TopK * 3;  // 粗排多取候选
        var knowledgeBaseId = request.KnowledgeBaseId ?? Guid.Empty;
        var candidates = await _retrievalService.RetrieveAsync(
            understanding.RewrittenQuery,
            knowledgeBaseId,
            retrievalTopK,
            cancellationToken);

        _logger.LogInformation("粗排检索到 {Count} 个候选知识块", candidates.Count);

        // 精排：用交叉编码器对 query-document 对重新打分
        List<Core.Entities.KnowledgeChunk> chunks;
        if (candidates.Count > 0)
        {
            var contents = candidates.Select(c => c.Content).ToList();
            var rerankScores = await _rerankService.ScoreAsync(
                understanding.RewrittenQuery, contents, cancellationToken);

            // 按 rerank 得分降序，取 topK
            chunks = candidates
                .Zip(rerankScores, (chunk, score) => new { chunk, score })
                .OrderByDescending(x => x.score)
                .Take(request.TopK)
                .Select(x =>
                {
                    x.chunk.Metadata = $"{{\"rerank_score\":{x.score:F4}}}";
                    return x.chunk;
                })
                .ToList();

            _logger.LogInformation("精排后保留 {Count} 个知识块", chunks.Count);
        }
        else
        {
            chunks = candidates;
        }

        // ── 第四步：拼装知识上下文 ──
        var knowledgeContext = BuildKnowledgeContext(chunks);

        // ── 第五步：构建 system prompt 并调用 LLM ──
        var systemPrompt = RagSystemPrompt.Replace("{knowledgeContext}", knowledgeContext);
        var answer = await _llmService.ChatAsync(systemPrompt, request.Question, cancellationToken);

        _logger.LogInformation("Agent 生成回答，长度: {Length}", answer.Length);

        // ── 第六步：组装响应 ──
        return new AgentResponse
        {
            Answer = answer,
            Sources = chunks.Select(c => Truncate(c.Content, 200)).ToList(),
            ChunksUsed = chunks.Count
        };
    }

    /// <summary>
    /// 将检索到的知识块拼装为上下文文本
    /// </summary>
    private static string BuildKnowledgeContext(List<Core.Entities.KnowledgeChunk> chunks)
    {
        if (chunks.Count == 0)
            return "（知识库中暂无相关内容）";

        var parts = new List<string>();
        for (int i = 0; i < chunks.Count; i++)
        {
            parts.Add($"[{i + 1}] {chunks[i].Content}");
        }

        return string.Join("\n\n", parts);
    }

    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }
}
