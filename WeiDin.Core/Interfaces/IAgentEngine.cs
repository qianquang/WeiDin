namespace WeiDin.Core.Interfaces;

/// <summary>
/// Agent 引擎接口 — 实现问答管道
///
/// 注意：当前实现本质上是 RAG 问答（检索增强生成），即"接收问题 → 检索知识 → 调用 LLM → 返回回答"。
/// 命名为 Agent 是为了预留未来扩展为真正 Agent 的能力（如自主推理、工具调用、多步规划等），
/// 但目前不包含 Agent 的核心特征（循环决策、工具使用、状态管理）。等价于之前 IAssistantChatService 的功能。
/// </summary>
public interface IAgentEngine
{
    /// <summary>
    /// 接受用户问题，检索知识库，调用 LLM 生成回答
    /// </summary>
    Task<AgentResponse> AskAsync(AgentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Agent 请求
/// </summary>
public class AgentRequest
{
    /// <summary>
    /// 用户问题
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// 检索的知识块数量，默认 5
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// 指定知识库 ID（可选，不指定则使用默认知识库）
    /// </summary>
    public Guid? KnowledgeBaseId { get; set; }

    /// <summary>
    /// 对话历史（可选），用于查询理解时结合上下文改写问题
    /// 格式：按时间顺序排列的消息文本列表，如 ["用户: 什么是RAG？", "助手: RAG是..."]
    /// </summary>
    public List<string>? ConversationHistory { get; set; }
}

/// <summary>
/// Agent 响应
/// </summary>
public class AgentResponse
{
    /// <summary>
    /// LLM 生成的回答
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// 引用的知识块内容摘要
    /// </summary>
    public List<string> Sources { get; set; } = new();

    /// <summary>
    /// 实际使用的知识块数量
    /// </summary>
    public int ChunksUsed { get; set; }
}
