namespace WeiDin.Application.DTOs;

public class MessageDto
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatar { get; set; }
    public Guid? ReceiverId { get; set; }
    public string? ReceiverName { get; set; }
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public List<MessageAttachmentDto> Attachments { get; set; } = new();
    public List<MessageStatusDto> Statuses { get; set; } = new();
}

public class CreateMessageDto
{
    public Guid? ReceiverId { get; set; }
    public Guid? GroupId { get; set; }
    public string MessageType { get; set; } = "Text";
    public string Content { get; set; } = string.Empty;
    public List<CreateMessageAttachmentDto>? Attachments { get; set; }
}

public class MessageAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ThumbnailPath { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateMessageAttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ThumbnailPath { get; set; }
}

public class MessageStatusDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class UpdateMessageStatusDto
{
    public string Status { get; set; } = string.Empty;
}
