using Microsoft.SemanticKernel;
using Volo.Abp.Application.Services;

namespace WeiDin.Application.Interfaces;

/// <summary>
/// LLM 聊天完成服务，封装 Semantic Kernel 与 DeepSeek API 调用。
/// </summary>
public interface IAssistantChatService : IApplicationService
{
    /// <summary>
    /// 获取已配置的 Kernel 实例，包含 MessageContext Skill 等插件。
    /// </summary>
    Kernel GetKernel();

    /// <summary>
    /// 获取指定会话的最近消息 JSON 上下文（通过 MessageContext Skill）。
    /// </summary>
    /// <param name="relationId">会话/关系标识</param>
    /// <param name="count">消息条数，默认 20</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息列表的 JSON 字符串</returns>
    Task<string> GetRecentMessagesJsonAsync(Guid relationId, int count = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// 基于消息上下文发送对话请求。
    /// 自动将指定会话的最近消息作为上下文拼接到 system prompt 中。
    /// </summary>
    /// <param name="relationId">会话标识，用于获取消息上下文</param>
    /// <param name="userPrompt">用户输入</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>LLM 回复内容</returns>
    Task<string> SendWithContextAsync(Guid relationId, string userPrompt, CancellationToken cancellationToken = default);
}
