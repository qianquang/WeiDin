using WeiDin.Core.Entities;
using WeiDin.Core.Inputs;

namespace WeiDin.Core.Interfaces;

/// <summary>
/// 基于分表的动态消息仓储。按 RelationId（即原 ConversationId）定位 Message_xxx / MessageAttachment_xxx / MessageStatus_xxx。
/// 查、改、删均依据 RelationId 直接访问对应分表。
/// </summary>
public interface IDynamicMessageRepository
{
    /// <summary>在指定关系分表中按消息 ID 查询，并加载 Attachments、MessageStatuses。</summary>
    Task<Message?> GetByIdAsync(Guid relationId, Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>分页查询某关系下的消息，并加载 Attachments、MessageStatuses。</summary>
    Task<IReadOnlyList<Message>> GetByRelationIdAsync(Guid relationId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>在指定关系分表中按关键词搜索消息，分页返回。</summary>
    Task<IReadOnlyList<Message>> SearchByRelationAsync(Guid relationId, string keyword, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>发送消息：确保分表存在，写入对应 Message_xxx；RelationId 即分表后缀。</summary>
    Task<Message> SendMessageAsync(Guid relationId, CreateMessageInput input, Guid senderId, CancellationToken cancellationToken = default);

    /// <summary>软删除消息，仅发送者可删；直接更新对应分表。</summary>
    Task<bool> DeleteMessageAsync(Guid relationId, Guid messageId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>更新某用户对某消息的状态，直接更新对应分表的 MessageStatus_xxx。</summary>
    Task<bool> UpdateMessageStatusAsync(Guid relationId, Guid messageId, Guid userId, string status, CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(Guid relationId, Guid messageId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> MarkAsDeliveredAsync(Guid relationId, Guid messageId, Guid userId, CancellationToken cancellationToken = default);
}
