using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace WeiDin.Core.Entities;

public class Message : Entity<Guid>
{
    public Message()
    {
        Id = Guid.NewGuid();
    }
    
    [Required]
    public Guid SenderId { get; set; }
    
    public Guid? ReceiverId { get; set; }
    
    public Guid? GroupId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string MessageType { get; set; } = "Text"; // Text, Image, Video, File
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    public bool IsDeleted { get; set; } = false;
    
    public DateTime? DeletedAt { get; set; }
    
    // 导航属性
    public virtual User Sender { get; set; } = null!;
    public virtual User? Receiver { get; set; }
    public virtual Group? Group { get; set; }
    public virtual ICollection<MessageStatus> MessageStatuses { get; set; } = new List<MessageStatus>();
    public virtual ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
}


