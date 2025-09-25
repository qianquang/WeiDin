using System.ComponentModel.DataAnnotations;

namespace WeiDin.Core.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? Nickname { get; set; }
    
    [MaxLength(200)]
    public string? Avatar { get; set; }
    
    [MaxLength(500)]
    public string? Bio { get; set; }
    
    public bool IsOnline { get; set; } = false;
    
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // 导航属性
    public virtual ICollection<Message> SentMessages { get; set; } = new List<Message>();
    public virtual ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
    public virtual ICollection<Friendship> Friendships { get; set; } = new List<Friendship>();
    public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
    public virtual ICollection<Blacklist> BlacklistedBy { get; set; } = new List<Blacklist>();
    public virtual ICollection<Blacklist> BlacklistedUsers { get; set; } = new List<Blacklist>();
}


