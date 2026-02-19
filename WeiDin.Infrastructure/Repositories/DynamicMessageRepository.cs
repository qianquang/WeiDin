using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WeiDin.Core.Entities;
using WeiDin.Core.Inputs;
using WeiDin.Core.Interfaces;
using WeiDin.Infrastructure.Data;

namespace WeiDin.Infrastructure.Repositories;

public sealed class DynamicMessageRepository : IDynamicMessageRepository
{
    private readonly WeiDinDbContext _db;
    private readonly IDynamicTableService _tables;

    public DynamicMessageRepository(WeiDinDbContext db, IDynamicTableService tables)
    {
        _db = db;
        _tables = tables;
    }

    public async Task<Message?> GetByIdAsync(Guid relationId, Guid messageId, CancellationToken cancellationToken = default)
    {
        await _tables.EnsureConversationTablesAsync(relationId, cancellationToken);
        var msgT = _tables.GetTableName("Message", relationId);
        var sql = $"SELECT * FROM [dbo].[{Escape(msgT)}] WHERE [Id] = '{messageId}' AND [IsDeleted] = 0";
        var list = await _db.Set<Message>().FromSqlRaw(sql).ToListAsync(cancellationToken);
        var msg = list.FirstOrDefault();
        if (msg != null)
            await LoadAttachmentsAndStatusesAsync(relationId, msg, cancellationToken);
        return msg;
    }

    public async Task<IReadOnlyList<Message>> GetByRelationIdAsync(Guid relationId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await _tables.EnsureConversationTablesAsync(relationId, cancellationToken);
        var msgT = _tables.GetTableName("Message", relationId);
        var offset = (page - 1) * pageSize;
        var sql = $@"SELECT * FROM [dbo].[{Escape(msgT)}] WHERE [IsDeleted] = 0 
ORDER BY [CreatedAt] DESC OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
        var list = await _db.Set<Message>().FromSqlRaw(sql).ToListAsync(cancellationToken);
        foreach (var m in list)
            await LoadAttachmentsAndStatusesAsync(relationId, m, cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<Message>> SearchByRelationAsync(Guid relationId, string keyword, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await _tables.EnsureConversationTablesAsync(relationId, cancellationToken);
        var msgT = _tables.GetTableName("Message", relationId);
        var kw = EscapeLiteral(keyword ?? "");
        var offset = (page - 1) * pageSize;
        var sql = $@"SELECT * FROM [dbo].[{Escape(msgT)}] 
WHERE [IsDeleted] = 0 AND [Content] LIKE N'%{kw}%' 
ORDER BY [CreatedAt] DESC OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
        var list = await _db.Set<Message>().FromSqlRaw(sql).ToListAsync(cancellationToken);
        foreach (var m in list)
            await LoadAttachmentsAndStatusesAsync(relationId, m, cancellationToken);
        return list;
    }

    public async Task<Message> SendMessageAsync(Guid relationId, CreateMessageInput input, Guid senderId, CancellationToken cancellationToken = default)
    {
        await _tables.EnsureConversationTablesAsync(relationId, cancellationToken);
        var msgId = Guid.NewGuid();
        var msgT = _tables.GetTableName("Message", relationId);
        var stT = _tables.GetTableName("MessageStatus", relationId);
        var atT = _tables.GetTableName("MessageAttachment", relationId);

        var receiverId = input.ReceiverId.HasValue ? $"'{input.ReceiverId}'" : "NULL";
        var groupId = input.GroupId.HasValue ? $"'{input.GroupId}'" : "NULL";
        var now = DateTime.UtcNow;
        var contentEsc = EscapeLiteral(input.Content);
        var typeEsc = EscapeLiteral(input.MessageType ?? "Text");
        var statusId = Guid.NewGuid();
        var attachmentEntities = new List<MessageAttachment>();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                $@"INSERT INTO [dbo].[{Escape(msgT)}] ([Id],[SenderId],[ReceiverId],[GroupId],[MessageType],[Content],[CreatedAt],[UpdatedAt],[IsDeleted],[DeletedAt])
VALUES ('{msgId}','{senderId}',{receiverId},{groupId},N'{typeEsc}',N'{contentEsc}','{now:O}',NULL,0,NULL)",
                cancellationToken);

            await _db.Database.ExecuteSqlRawAsync(
                $@"INSERT INTO [dbo].[{Escape(stT)}] ([Id],[MessageId],[UserId],[Status],[CreatedAt])
VALUES ('{statusId}','{msgId}','{senderId}',N'Sent','{now:O}')",
                cancellationToken);

            if (input.Attachments != null)
            {
                foreach (var a in input.Attachments)
                {
                    var aid = Guid.NewGuid();
                    var fn = EscapeLiteral(a.FileName);
                    var fp = EscapeLiteral(a.FilePath);
                    var ft = EscapeLiteral(a.FileType);
                    var thumb = a.ThumbnailPath != null ? $"N'{EscapeLiteral(a.ThumbnailPath)}'" : "NULL";
                    await _db.Database.ExecuteSqlRawAsync(
                        $@"INSERT INTO [dbo].[{Escape(atT)}] ([Id],[MessageId],[FileName],[FilePath],[FileType],[FileSize],[ThumbnailPath],[CreatedAt])
VALUES ('{aid}','{msgId}',N'{fn}',N'{fp}',N'{ft}',{a.FileSize},{thumb},'{now:O}')",
                        cancellationToken);
                    var att = new MessageAttachment
                    {
                        MessageId = msgId,
                        FileName = a.FileName,
                        FilePath = a.FilePath,
                        FileType = a.FileType,
                        FileSize = a.FileSize,
                        ThumbnailPath = a.ThumbnailPath,
                        CreatedAt = now
                    };
                    SetEntityId(att, aid);
                    attachmentEntities.Add(att);
                }
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }

        var status = new MessageStatus
        {
            MessageId = msgId,
            UserId = senderId,
            Status = "Sent",
            CreatedAt = now
        };
        SetEntityId(status, statusId);

        var msg = new Message
        {
            SenderId = senderId,
            ReceiverId = input.ReceiverId,
            GroupId = input.GroupId,
            MessageType = input.MessageType ?? "Text",
            Content = input.Content,
            CreatedAt = now,
            IsDeleted = false,
            Attachments = attachmentEntities,
            MessageStatuses = new List<MessageStatus> { status }
        };
        SetEntityId(msg, msgId);
        return msg;
    }

    public async Task CreateGroupMessageStatusesAsync(Guid relationId, Guid messageId, Guid senderId, IEnumerable<Guid> memberIds, CancellationToken cancellationToken = default)
    {
        await _tables.EnsureConversationTablesAsync(relationId, cancellationToken);
        var stT = _tables.GetTableName("MessageStatus", relationId);
        var now = DateTime.UtcNow;

        // 过滤掉发送者，只为其他成员创建状态记录
        var membersToCreate = memberIds.Where(id => id != senderId).ToList();
        if (membersToCreate.Count == 0)
            return;

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await _db.Database.OpenConnectionAsync(cancellationToken);
            }

            foreach (var memberId in membersToCreate)
            {
                // 检查是否已存在状态记录（避免重复创建）
                await using var checkCmd = conn.CreateCommand();
                checkCmd.Transaction = tx.GetDbTransaction();
                checkCmd.CommandText = $@"SELECT COUNT(*) FROM [dbo].[{Escape(stT)}] 
WHERE [MessageId] = '{messageId}' AND [UserId] = '{memberId}'";
                
                var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken)) > 0;
                if (!exists)
                {
                    // 为每个成员创建状态记录（初始状态为 Sent，表示消息已发送到群组）
                    var statusId = Guid.NewGuid();
                    await using var insertCmd = conn.CreateCommand();
                    insertCmd.Transaction = tx.GetDbTransaction();
                    insertCmd.CommandText = $@"INSERT INTO [dbo].[{Escape(stT)}] ([Id],[MessageId],[UserId],[Status],[CreatedAt])
VALUES ('{statusId}','{messageId}','{memberId}',N'Sent','{now:O}')";
                    await insertCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> DeleteMessageAsync(Guid relationId, Guid messageId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _tables.EnsureConversationTablesAsync(relationId, cancellationToken);
        var msgT = _tables.GetTableName("Message", relationId);
        var now = DateTime.UtcNow;
        var n = await _db.Database.ExecuteSqlRawAsync(
            $@"UPDATE [dbo].[{Escape(msgT)}] SET [IsDeleted]=1,[DeletedAt]='{now:O}',[UpdatedAt]='{now:O}' 
WHERE [Id]='{messageId}' AND [SenderId]='{userId}'",
            cancellationToken);
        return n > 0;
    }

    public async Task<bool> UpdateMessageStatusAsync(Guid relationId, Guid messageId, Guid userId, string status, CancellationToken cancellationToken = default)
    {
        await _tables.EnsureConversationTablesAsync(relationId, cancellationToken);
        var msgT = _tables.GetTableName("Message", relationId);
        var stT = _tables.GetTableName("MessageStatus", relationId);
        var statusEsc = EscapeLiteral(status);
        var now = DateTime.UtcNow;

        // 使用事务确保数据一致性
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await _db.Database.OpenConnectionAsync(cancellationToken);
            }

            // 验证消息是否存在，且当前用户是接收方（私聊）或群组成员（群组消息）
            await using var checkCmd = conn.CreateCommand();
            checkCmd.Transaction = tx.GetDbTransaction();
            checkCmd.CommandText = $@"SELECT COUNT(*) FROM [dbo].[{Escape(msgT)}] 
WHERE [Id] = '{messageId}' 
  AND [IsDeleted] = 0 
  AND [SenderId] <> '{userId}'
  AND (
    -- 私聊消息：接收方是当前用户
    ([ReceiverId] = '{userId}' AND [GroupId] IS NULL)
    OR
    -- 群组消息：群组ID匹配（通过 relationId 判断，relationId 对于群组消息就是 GroupId）
    ([GroupId] = '{relationId}')
  )";
            var isValid = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken)) > 0;
            if (!isValid)
            {
                await tx.RollbackAsync(cancellationToken);
                return false;
            }

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx.GetDbTransaction();
            cmd.CommandText = $@"SELECT [Id] FROM [dbo].[{Escape(stT)}] WHERE [MessageId]='{messageId}' AND [UserId]='{userId}'";
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await r.ReadAsync(cancellationToken))
            {
                var id = r.GetGuid(0);
                await r.CloseAsync();
                await using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = tx.GetDbTransaction();
                updateCmd.CommandText = $@"UPDATE [dbo].[{Escape(stT)}] SET [Status]=N'{statusEsc}' WHERE [Id]='{id}'";
                await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await r.CloseAsync();
                var newId = Guid.NewGuid();
                await using var insertCmd = conn.CreateCommand();
                insertCmd.Transaction = tx.GetDbTransaction();
                insertCmd.CommandText = $@"INSERT INTO [dbo].[{Escape(stT)}] ([Id],[MessageId],[UserId],[Status],[CreatedAt])
VALUES ('{newId}','{messageId}','{userId}',N'{statusEsc}','{now:O}')";
                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            
            await tx.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> UpdateMessageStatusDirectlyAsync(Guid relationId, Guid messageId, Guid userId, string status, CancellationToken cancellationToken = default)
    {
        await _tables.EnsureConversationTablesAsync(relationId, cancellationToken);
        var stT = _tables.GetTableName("MessageStatus", relationId);
        var statusEsc = EscapeLiteral(status);
        var now = DateTime.UtcNow;

        // 使用事务确保数据一致性
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await _db.Database.OpenConnectionAsync(cancellationToken);
            }

            // 直接更新或插入状态记录，不验证接收方（用于处理 ReceiverId 为 NULL 的旧消息）
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx.GetDbTransaction();
            cmd.CommandText = $@"SELECT [Id] FROM [dbo].[{Escape(stT)}] WHERE [MessageId]='{messageId}' AND [UserId]='{userId}'";
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await r.ReadAsync(cancellationToken))
            {
                var id = r.GetGuid(0);
                await r.CloseAsync();
                await using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = tx.GetDbTransaction();
                updateCmd.CommandText = $@"UPDATE [dbo].[{Escape(stT)}] SET [Status]=N'{statusEsc}' WHERE [Id]='{id}'";
                await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await r.CloseAsync();
                var newId = Guid.NewGuid();
                await using var insertCmd = conn.CreateCommand();
                insertCmd.Transaction = tx.GetDbTransaction();
                insertCmd.CommandText = $@"INSERT INTO [dbo].[{Escape(stT)}] ([Id],[MessageId],[UserId],[Status],[CreatedAt])
VALUES ('{newId}','{messageId}','{userId}',N'{statusEsc}','{now:O}')";
                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            
            await tx.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task<bool> MarkAsReadAsync(Guid relationId, Guid messageId, Guid userId, CancellationToken cancellationToken = default)
        => UpdateMessageStatusAsync(relationId, messageId, userId, "Read", cancellationToken);

    public Task<bool> MarkAsDeliveredAsync(Guid relationId, Guid messageId, Guid userId, CancellationToken cancellationToken = default)
        => UpdateMessageStatusAsync(relationId, messageId, userId, "Delivered", cancellationToken);

    public async Task<IReadOnlyList<Guid>> MarkAllAsReadAsync(Guid relationId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _tables.EnsureConversationTablesAsync(relationId, cancellationToken);
        var msgT = _tables.GetTableName("Message", relationId);
        var stT = _tables.GetTableName("MessageStatus", relationId);
        var statusEsc = EscapeLiteral("Read");
        var now = DateTime.UtcNow;

        // 使用事务确保数据一致性
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await _db.Database.OpenConnectionAsync(cancellationToken);
            }

            // 1. 查询所有未读消息（未删除）：
            //    - 私聊消息：接收方是当前用户且发送方不是当前用户
            //    - 群组消息：群组ID匹配且发送方不是当前用户，且（没有状态记录 || 有状态记录但状态不是Read）
            // 注意：排除已有 Read 状态的消息
            await using var queryCmd = conn.CreateCommand();
            queryCmd.Transaction = tx.GetDbTransaction();
            queryCmd.CommandText = $@"SELECT [Id], [SenderId] FROM [dbo].[{Escape(msgT)}] m
WHERE m.[IsDeleted] = 0 
  AND m.[SenderId] <> '{userId}'
  AND (
    -- 私聊消息：接收方是当前用户
    (m.[ReceiverId] = '{userId}' AND m.[GroupId] IS NULL)
    OR
    -- 群组消息：群组ID匹配，且（没有状态记录 || 有状态记录但状态不是Read）
    (m.[GroupId] = '{relationId}' AND (
      NOT EXISTS (
        SELECT 1 FROM [dbo].[{Escape(stT)}] s 
        WHERE s.[MessageId] = m.[Id] 
          AND s.[UserId] = '{userId}'
      )
      OR EXISTS (
        SELECT 1 FROM [dbo].[{Escape(stT)}] s 
        WHERE s.[MessageId] = m.[Id] 
          AND s.[UserId] = '{userId}'
          AND s.[Status] <> N'Read'
      )
    ))
  )
  AND NOT EXISTS (
    SELECT 1 FROM [dbo].[{Escape(stT)}] s 
    WHERE s.[MessageId] = m.[Id] 
      AND s.[UserId] = '{userId}' 
      AND s.[Status] = N'Read'
  )";
            
            var unreadMessageIds = new List<Guid>();
            var senderIds = new HashSet<Guid>();
            
            await using var reader = await queryCmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var msgId = reader.GetGuid(0);
                var senderId = reader.GetGuid(1);
                unreadMessageIds.Add(msgId);
                senderIds.Add(senderId);
            }
            await reader.CloseAsync();

            if (unreadMessageIds.Count == 0)
            {
                await tx.CommitAsync(cancellationToken);
                return Array.Empty<Guid>();
            }

            // 2. 批量更新或插入 MessageStatus 记录（使用事务中的连接）
            var markedMessageIds = new List<Guid>();
            
            foreach (var messageId in unreadMessageIds)
            {
                // 检查是否已有状态记录（使用 MERGE 语句更高效，但这里用简单方式）
                await using var checkCmd = conn.CreateCommand();
                checkCmd.Transaction = tx.GetDbTransaction();
                checkCmd.CommandText = $@"SELECT [Id] FROM [dbo].[{Escape(stT)}] 
WHERE [MessageId] = '{messageId}' AND [UserId] = '{userId}'";
                
                await using var checkReader = await checkCmd.ExecuteReaderAsync(cancellationToken);
                if (await checkReader.ReadAsync(cancellationToken))
                {
                    var statusId = checkReader.GetGuid(0);
                    await checkReader.CloseAsync();
                    // 更新现有记录（确保更新的是接收方的状态记录）
                    await using var updateCmd = conn.CreateCommand();
                    updateCmd.Transaction = tx.GetDbTransaction();
                    updateCmd.CommandText = $@"UPDATE [dbo].[{Escape(stT)}] SET [Status] = N'{statusEsc}' WHERE [Id] = '{statusId}' AND [UserId] = '{userId}'";
                    var updateResult = await updateCmd.ExecuteNonQueryAsync(cancellationToken);
                    if (updateResult == 0)
                    {
                        // 如果更新失败（可能是状态记录不是接收方的），则插入新记录
                        var newStatusId = Guid.NewGuid();
                        await using var insertCmd = conn.CreateCommand();
                        insertCmd.Transaction = tx.GetDbTransaction();
                        insertCmd.CommandText = $@"INSERT INTO [dbo].[{Escape(stT)}] ([Id],[MessageId],[UserId],[Status],[CreatedAt])
VALUES ('{newStatusId}','{messageId}','{userId}',N'{statusEsc}','{now:O}')";
                        await insertCmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
                else
                {
                    await checkReader.CloseAsync();
                    // 插入新记录
                    var newStatusId = Guid.NewGuid();
                    await using var insertCmd = conn.CreateCommand();
                    insertCmd.Transaction = tx.GetDbTransaction();
                    insertCmd.CommandText = $@"INSERT INTO [dbo].[{Escape(stT)}] ([Id],[MessageId],[UserId],[Status],[CreatedAt])
VALUES ('{newStatusId}','{messageId}','{userId}',N'{statusEsc}','{now:O}')";
                    await insertCmd.ExecuteNonQueryAsync(cancellationToken);
                }
                
                markedMessageIds.Add(messageId);
            }

            await tx.CommitAsync(cancellationToken);
            return markedMessageIds;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> GetUnreadCountAsync(Guid relationId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _tables.EnsureConversationTablesAsync(relationId, cancellationToken);
        var msgT = _tables.GetTableName("Message", relationId);
        var stT = _tables.GetTableName("MessageStatus", relationId);

        var conn = _db.Database.GetDbConnection();
        await _db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            // 查询未读消息数量：
            // 1. 私聊消息：接收方是当前用户且发送方不是当前用户
            // 2. 群组消息：群组ID匹配且发送方不是当前用户，且（没有状态记录 || 有状态记录但状态不是Read）
            await using var queryCmd = conn.CreateCommand();
            queryCmd!.CommandText = $@"SELECT COUNT(*) FROM [dbo].[{Escape(msgT)}] m
WHERE m.[IsDeleted] = 0 
  AND m.[SenderId] <> '{userId}'
  AND (
    -- 私聊消息：接收方是当前用户
    (m.[ReceiverId] = '{userId}' AND m.[GroupId] IS NULL)
    OR
    -- 群组消息：群组ID匹配，且（没有状态记录 || 有状态记录但状态不是Read）
    (m.[GroupId] = '{relationId}' AND (
      NOT EXISTS (
        SELECT 1 FROM [dbo].[{Escape(stT)}] s 
        WHERE s.[MessageId] = m.[Id] 
          AND s.[UserId] = '{userId}'
      )
      OR EXISTS (
        SELECT 1 FROM [dbo].[{Escape(stT)}] s 
        WHERE s.[MessageId] = m.[Id] 
          AND s.[UserId] = '{userId}'
          AND s.[Status] <> N'Read'
      )
    ))
  )
  AND NOT EXISTS (
    SELECT 1 FROM [dbo].[{Escape(stT)}] s 
    WHERE s.[MessageId] = m.[Id] 
      AND s.[UserId] = '{userId}' 
      AND s.[Status] = N'Read'
  )";
            
            var result = await queryCmd.ExecuteScalarAsync(cancellationToken);
            return result != null ? Convert.ToInt32(result) : 0;
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    public async Task<IReadOnlyList<Message>> GetMessagesWithNullReceiverIdAsync(Guid relationId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _tables.EnsureConversationTablesAsync(relationId, cancellationToken);
        var msgT = _tables.GetTableName("Message", relationId);
        var stT = _tables.GetTableName("MessageStatus", relationId);

        // 查询 ReceiverId 为 NULL 且发送方不是当前用户的消息（未删除）
        // 并且没有已读状态记录的消息
        var sql = $@"SELECT * FROM [dbo].[{Escape(msgT)}] m
WHERE m.[IsDeleted] = 0 
  AND m.[ReceiverId] IS NULL
  AND m.[GroupId] IS NULL
  AND m.[SenderId] <> '{userId}'
  AND NOT EXISTS (
    SELECT 1 FROM [dbo].[{Escape(stT)}] s 
    WHERE s.[MessageId] = m.[Id] 
      AND s.[UserId] = '{userId}' 
      AND s.[Status] = N'Read'
  )";
        
        var messages = await _db.Set<Message>().FromSqlRaw(sql).ToListAsync(cancellationToken);
        return messages;
    }

    private async Task LoadAttachmentsAndStatusesAsync(Guid relationId, Message msg, CancellationToken ct)
    {
        var atT = _tables.GetTableName("MessageAttachment", relationId);
        var stT = _tables.GetTableName("MessageStatus", relationId);
        var msgId = msg.Id;

        var aSql = $"SELECT * FROM [dbo].[{Escape(atT)}] WHERE [MessageId] = '{msgId}'";
        var attachments = await _db.Set<MessageAttachment>().FromSqlRaw(aSql).ToListAsync(ct);
        msg.Attachments = attachments;

        var sSql = $"SELECT * FROM [dbo].[{Escape(stT)}] WHERE [MessageId] = '{msgId}'";
        var statuses = await _db.Set<MessageStatus>().FromSqlRaw(sSql).ToListAsync(ct);
        msg.MessageStatuses = statuses;
    }

    private static void SetEntityId(object entity, Guid id)
    {
        var prop = entity.GetType().GetProperty("Id");
        prop?.SetValue(entity, id);
    }

    private static string Escape(string id) => id.Replace("]", "]]");
    private static string EscapeLiteral(string s) => (s ?? "").Replace("'", "''");
}
