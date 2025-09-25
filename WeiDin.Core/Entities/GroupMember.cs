using System.ComponentModel.DataAnnotations;

namespace WeiDin.Core.Entities;

public class GroupMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid GroupId { get; set; }
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = "Member"; // Owner, Admin, Member
    
    [MaxLength(50)]
    public string? Nickname { get; set; }
    
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LeftAt { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // 导航属性
    public virtual Group Group { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}



