namespace WeiDin.Core.Interfaces;

/// <summary>
/// 动态消息分表管理：按 ConversationId 创建/同步 Message、MessageAttachment、MessageStatus 分表。
/// </summary>
public interface IDynamicTableService
{
    /// <summary>确保指定会话的三张分表存在，不存在则按主表结构创建。</summary>
    Task EnsureConversationTablesAsync(Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>获取分表名。baseName 为 Message / MessageAttachment / MessageStatus，GUID 不含连字符。</summary>
    string GetTableName(string baseName, Guid conversationId);

    /// <summary>将主表结构同步到所有同名分表。baseName 同上。</summary>
    Task SyncTableStructureAsync(string baseName, CancellationToken cancellationToken = default);

    /// <summary>获取所有已存在分表的 ConversationId（用于批量同步）。</summary>
    Task<IReadOnlyList<Guid>> GetAllConversationIdsAsync(CancellationToken cancellationToken = default);
}
