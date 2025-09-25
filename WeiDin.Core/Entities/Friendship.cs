using System.ComponentModel.DataAnnotations;

namespace WeiDin.Core.Entities;

public class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
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
    
    // 导航属性
    public virtual User User { get; set; } = null!;
    public virtual User Friend { get; set; } = null!;
}



