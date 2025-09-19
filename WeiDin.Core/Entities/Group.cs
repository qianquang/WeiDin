using System.ComponentModel.DataAnnotations;

namespace WeiDin.Core.Entities;

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(200)]
    public string? Avatar { get; set; }
    
    [Required]
    public Guid OwnerId { get; set; }
    
    [MaxLength(1000)]
    public string? Announcement { get; set; }
    
    public int MaxMembers { get; set; } = 500;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // 导航属性
    public virtual User Owner { get; set; } = null!;
    public virtual ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}
