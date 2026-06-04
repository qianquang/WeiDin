namespace WeiDin.Core.Enums;

/// <summary>
/// 文档解析状态枚举
/// </summary>
public enum ParseStatus
{
    /// <summary>
    /// 等待解析
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 正在解析
    /// </summary>
    Processing = 1,

    /// <summary>
    /// 解析完成
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 解析失败
    /// </summary>
    Failed = 3
}
