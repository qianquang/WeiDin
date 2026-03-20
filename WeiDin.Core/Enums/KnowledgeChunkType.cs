namespace WeiDin.Core.Enums;

/// <summary>
/// 知识块类型枚举
/// </summary>
public enum KnowledgeChunkType
{
    /// <summary>
    /// 聊天消息
    /// </summary>
    ChatMessage = 0,

    /// <summary>
    /// 用户个人信息
    /// </summary>
    UserProfile = 1,

    /// <summary>
    /// 群组信息
    /// </summary>
    GroupInfo = 2,

    /// <summary>
    /// 文件内容
    /// </summary>
    FileContent = 3,

    /// <summary>
    /// 自定义知识
    /// </summary>
    Custom = 99
}
