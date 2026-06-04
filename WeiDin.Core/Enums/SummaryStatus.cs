namespace WeiDin.Core.Enums;

/// <summary>
/// 摘要生成状态枚举
/// </summary>
public enum SummaryStatus
{
    /// <summary>
    /// 未生成摘要
    /// </summary>
    None = 0,

    /// <summary>
    /// 等待生成
    /// </summary>
    Pending = 1,

    /// <summary>
    /// 摘要已生成
    /// </summary>
    Completed = 2
}
