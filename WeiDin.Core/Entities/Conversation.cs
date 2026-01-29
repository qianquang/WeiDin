using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;
using WeiDin.Core.Enums;

namespace WeiDin.Core.Entities;

/// <summary>
/// 消息关系表：每段好友或群聊关系一条记录，对应一组动态分表（Message_xxx, MessageAttachment_xxx, MessageStatus_xxx）
/// </summary>
public class Conversation : Entity<Guid>
{
    /// <summary>统一字段：FriendshipId 或 GroupId</summary>
    [Required]
    public Guid RelationId { get; set; }

    [Required]
    public RelationType RelationType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
