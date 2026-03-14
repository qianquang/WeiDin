using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;

namespace WeiDin.Application.Plugins;

/// <summary>
/// Skill: 将指定会话的最近消息序列化为 JSON，作为 LLM prompt 的上下文。
/// </summary>
public class MessageContextPlugin
{
    private readonly IMessageService _messageService;

    public MessageContextPlugin(IMessageService messageService)
    {
        _messageService = messageService;
    }

    [KernelFunction("get_recent_messages_as_json")]
    [Description("获取指定会话的最近消息，并序列化为 JSON 字符串，供 LLM 作为上下文使用。")]
    public async Task<string> GetRecentMessagesAsJsonAsync(
        [Description("会话/关系标识，私聊为 Friendship 的 ConversationId，群聊为 GroupId")]
        Guid relationId,
        [Description("获取的消息条数，默认 20")]
        int count = 20,
        CancellationToken cancellationToken = default)
    {
        var messages = await _messageService.GetByRelationIdAsync(relationId, page: 1, pageSize: count);
        var items = messages
            .Select(m => new MessageContextItem(
                m.Id,
                m.SenderName ?? string.Empty,
                m.Content ?? string.Empty,
                m.MessageType ?? "Text",
                m.CreatedAt))
            .Reverse() // GetByRelationIdAsync 返回 CreatedAt DESC，反转后为时间正序
            .ToList();

        return JsonSerializer.Serialize(items, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    private record MessageContextItem(
        Guid Id,
        string SenderName,
        string Content,
        string MessageType,
        DateTime CreatedAt);
}
