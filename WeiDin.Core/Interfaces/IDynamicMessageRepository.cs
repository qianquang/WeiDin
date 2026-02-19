using WeiDin.Core.Entities;
using WeiDin.Core.Inputs;

namespace WeiDin.Core.Interfaces;

/// <summary>
/// 基于分表的动态消息仓储。按 RelationId定位 Message_xxx / MessageAttachment_xxx / MessageStatus_xxx。
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
    
    /// <summary>为群组消息创建所有成员的状态记录（除了发送者）。</summary>
    Task CreateGroupMessageStatusesAsync(Guid relationId, Guid messageId, Guid senderId, IEnumerable<Guid> memberIds, CancellationToken cancellationToken = default);

    /// <summary>软删除消息，仅发送者可删；直接更新对应分表。</summary>
    Task<bool> DeleteMessageAsync(Guid relationId, Guid messageId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>更新某用户对某消息的状态，直接更新对应分表的 MessageStatus_xxx。</summary>
    Task<bool> UpdateMessageStatusAsync(Guid relationId, Guid messageId, Guid userId, string status, CancellationToken cancellationToken = default);
    
    /// <summary>直接更新消息状态（不验证接收方，用于处理 ReceiverId 为 NULL 的旧消息）。</summary>
    Task<bool> UpdateMessageStatusDirectlyAsync(Guid relationId, Guid messageId, Guid userId, string status, CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(Guid relationId, Guid messageId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> MarkAsDeliveredAsync(Guid relationId, Guid messageId, Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>批量标记某个关系下所有未读消息为已读（仅标记接收方为当前用户且发送方不是当前用户的消息）。</summary>
    Task<IReadOnlyList<Guid>> MarkAllAsReadAsync(Guid relationId, Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>获取某个关系下未读消息数量（仅统计接收方为当前用户且发送方不是当前用户的消息）。</summary>
    Task<int> GetUnreadCountAsync(Guid relationId, Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>查询 ReceiverId 为 NULL 且发送方不是当前用户的消息列表（用于处理旧数据）。</summary>
    Task<IReadOnlyList<Message>> GetMessagesWithNullReceiverIdAsync(Guid relationId, Guid userId, CancellationToken cancellationToken = default);
}
