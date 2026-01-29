using System.Reflection;
using Microsoft.EntityFrameworkCore;
using WeiDin.Core.Entities;
using WeiDin.Core.Inputs;
using WeiDin.Core.Interfaces;
using WeiDin.Infrastructure.Data;

namespace WeiDin.Infrastructure.Services;

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

        var checkSql = $"SELECT COUNT(*) FROM [dbo].[{Escape(msgT)}] WHERE [Id] = '{messageId}'";
        var conn = _db.Database.GetDbConnection();
        await _db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var checkCmd = conn.CreateCommand();
            checkCmd!.CommandText = checkSql;
            var messageExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken)) > 0;
            if (!messageExists) return false;

            await using var cmd = conn.CreateCommand();
            cmd!.CommandText = $@"SELECT [Id] FROM [dbo].[{Escape(stT)}] WHERE [MessageId]='{messageId}' AND [UserId]='{userId}'";
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await r.ReadAsync(cancellationToken))
            {
                var id = r.GetGuid(0);
                await r.CloseAsync();
                await _db.Database.ExecuteSqlRawAsync(
                    $@"UPDATE [dbo].[{Escape(stT)}] SET [Status]=N'{statusEsc}' WHERE [Id]='{id}'",
                    cancellationToken);
            }
            else
            {
                await r.CloseAsync();
                var newId = Guid.NewGuid();
                await _db.Database.ExecuteSqlRawAsync(
                    $@"INSERT INTO [dbo].[{Escape(stT)}] ([Id],[MessageId],[UserId],[Status],[CreatedAt])
VALUES ('{newId}','{messageId}','{userId}',N'{statusEsc}','{now:O}')",
                    cancellationToken);
            }
            return true;
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    public Task<bool> MarkAsReadAsync(Guid relationId, Guid messageId, Guid userId, CancellationToken cancellationToken = default)
        => UpdateMessageStatusAsync(relationId, messageId, userId, "Read", cancellationToken);

    public Task<bool> MarkAsDeliveredAsync(Guid relationId, Guid messageId, Guid userId, CancellationToken cancellationToken = default)
        => UpdateMessageStatusAsync(relationId, messageId, userId, "Delivered", cancellationToken);

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
