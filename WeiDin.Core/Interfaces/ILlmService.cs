namespace WeiDin.Core.Interfaces;

/// <summary>
/// LLM 聊天服务 — 负责调用大语言模型 API
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// 发送对话请求并获取回复
    /// </summary>
    /// <param name="systemPrompt">系统提示词</param>
    /// <param name="userMessage">用户消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>LLM 回复文本</returns>
    Task<string> ChatAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default);
}
