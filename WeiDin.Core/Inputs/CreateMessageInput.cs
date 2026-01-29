namespace WeiDin.Core.Inputs;

/// <summary>
/// 领域层创建消息输入，供 IDynamicMessageRepository 使用。
/// </summary>
public class CreateMessageInput
{
    public Guid? ReceiverId { get; set; }
    public Guid? GroupId { get; set; }
    public string MessageType { get; set; } = "Text";
    public string Content { get; set; } = string.Empty;
    public List<CreateMessageAttachmentInput>? Attachments { get; set; }
}
