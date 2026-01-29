namespace WeiDin.Core.Inputs;

/// <summary>
/// 领域层创建消息附件输入，供 IDynamicMessageRepository 使用。
/// </summary>
public class CreateMessageAttachmentInput
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ThumbnailPath { get; set; }
}
