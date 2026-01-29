using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace WeiDin.Core.Entities;

public class Friendship : Entity<Guid>
{
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public Guid FriendId { get; set; }
    
    [MaxLength(50)]
    public string? GroupName { get; set; } // 好友分组
    
    [MaxLength(50)]
    public string? Remark { get; set; } // 备注
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    public bool IsActive { get; set; } = true;

    /// <summary>消息关系 ID，与发起人 Friendship.Id 一致；接受申请后双向记录共用此值</summary>
    public Guid? ConversationId { get; set; }
    
    // 导航属性
    public virtual User User { get; set; } = null!;
    public virtual User Friend { get; set; } = null!;
}



