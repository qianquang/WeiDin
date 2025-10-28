using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace WeiDin.Core.Entities;

public class Blacklist : Entity<Guid>
{
    public Blacklist()
    {
        Id = Guid.NewGuid();
    }
    
    [Required]
    public Guid UserId { get; set; } // 拉黑者
    
    [Required]
    public Guid BlockedUserId { get; set; } // 被拉黑者
    
    [MaxLength(200)]
    public string? Reason { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // 导航属性
    public virtual User User { get; set; } = null!;
    public virtual User BlockedUser { get; set; } = null!;
}



