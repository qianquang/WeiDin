using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;

namespace WeiDin.Core.Entities;

public class MessageAttachment : Entity<Guid>
{
    [Required]
    public Guid MessageId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string FileType { get; set; } = string.Empty;
    
    public long FileSize { get; set; }
    
    [MaxLength(200)]
    public string? ThumbnailPath { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // 导航属性
    public virtual Message Message { get; set; } = null!;
}



