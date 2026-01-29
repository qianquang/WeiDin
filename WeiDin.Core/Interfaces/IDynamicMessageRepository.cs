using WeiDin.Core.Entities;
using WeiDin.Core.Inputs;

namespace WeiDin.Core.Interfaces;

/// <summary>
/// 基于分表的动态消息仓储。按 ConversationId 读写 Message_xxx / MessageAttachment_xxx / MessageStatus_xxx。
/// </summary>
public interface IDynamicMessageRepository
{
    /// <summary>通过消息 ID 查找（先查索引再查分表），并加载 Attachments、MessageStatuses。</summary>
    Task<Message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>分页查询会话消息，并加载 Attachments、MessageStatuses。</summary>
    Task<IReadOnlyList<Message>> GetByConversationAsync(Guid conversationId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>发送消息并写入索引；确保分表存在。返回领域实体 Message。</summary>
    Task<Message> SendMessageAsync(Guid conversationId, CreateMessageInput input, Guid senderId, CancellationToken cancellationToken = default);

    /// <summary>软删除消息（先查索引再更新分表），仅发送者可删。</summary>
    Task<bool> DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>更新某用户对某消息的状态（先查索引再更新 MessageStatus_xxx）。</summary>
    Task<bool> UpdateMessageStatusAsync(Guid messageId, Guid userId, string status, CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> MarkAsDeliveredAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);
}
