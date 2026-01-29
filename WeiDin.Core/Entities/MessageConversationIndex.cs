using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace WeiDin.Core.Entities;

/// <summary>
/// 消息 ID -> 会话 ID 索引，用于在仅有消息 ID 时定位分表（如 GetById / Delete / MarkAsRead）。
/// Id = MessageId，ConversationId 为对应分表会话。
/// </summary>
public class MessageConversationIndex : Entity<Guid>
{
    [Required]
    public Guid ConversationId { get; set; }
}
