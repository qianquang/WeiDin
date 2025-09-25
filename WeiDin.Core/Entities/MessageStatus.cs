using System.ComponentModel.DataAnnotations;

namespace WeiDin.Core.Entities;

public class MessageStatus
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid MessageId { get; set; }
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Sent"; // Sent, Delivered, Read
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // 导航属性
    public virtual Message Message { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}



