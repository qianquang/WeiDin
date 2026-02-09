using Microsoft.EntityFrameworkCore;
using WeiDin.Core.Interfaces;
using WeiDin.Infrastructure.Data;

namespace WeiDin.Infrastructure.Repositories;

public sealed class DynamicTableService : IDynamicTableService
{
    private readonly WeiDinDbContext _db;
    private const string MessageBase = "Message";
    private const string MessageAttachmentBase = "MessageAttachment";
    private const string MessageStatusBase = "MessageStatus";
    private static readonly string[] BaseNames = { MessageBase, MessageAttachmentBase, MessageStatusBase };

    private static readonly Dictionary<string, string> BaseToMainTable = new(StringComparer.OrdinalIgnoreCase)
    {
        [MessageBase] = "Messages",
        [MessageAttachmentBase] = "MessageAttachments",
        [MessageStatusBase] = "MessageStatuses"
    };

    public DynamicTableService(WeiDinDbContext db)
    {
        _db = db;
    }

    public string GetTableName(string baseName, Guid conversationId)
    {
        var suffix = conversationId.ToString("N");
        return $"{baseName}_{suffix}";
    }

    public async Task EnsureConversationTablesAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        foreach (var baseName in BaseNames)
        {
            var main = BaseToMainTable[baseName];
            var shard = GetTableName(baseName, conversationId);
            var sql = $@"
IF NOT EXISTS (SELECT 1 FROM sys.tables t
  JOIN sys.schemas s ON t.schema_id = s.schema_id
  WHERE s.name = N'dbo' AND t.name = N'{EscapeSqlLiteral(shard)}')
BEGIN
  SELECT * INTO [dbo].[{EscapeSqlId(shard)}] FROM [dbo].[{EscapeSqlId(main)}] WHERE 1 = 0;
END";
            await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }

    public async Task SyncTableStructureAsync(string baseName, CancellationToken cancellationToken = default)
    {
        if (!BaseToMainTable.TryGetValue(baseName, out var mainTable))
            throw new ArgumentException($"Unknown base name: {baseName}. Use {MessageBase}, {MessageAttachmentBase}, or {MessageStatusBase}.", nameof(baseName));

        var prefix = $"{baseName}_";
        var ids = await GetAllConversationIdsFromTablesAsync(prefix, cancellationToken);
        foreach (var id in ids)
        {
            var shard = GetTableName(baseName, id);
            await SyncShardToMainAsync(mainTable, shard, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Guid>> GetAllConversationIdsAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllConversationIdsFromTablesAsync($"{MessageBase}_", cancellationToken);
    }

    public async Task DeleteConversationTablesAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        foreach (var baseName in BaseNames)
        {
            var shard = GetTableName(baseName, conversationId);
            var sql = $@"
IF EXISTS (SELECT 1 FROM sys.tables t
  JOIN sys.schemas s ON t.schema_id = s.schema_id
  WHERE s.name = N'dbo' AND t.name = N'{EscapeSqlLiteral(shard)}')
BEGIN
  DROP TABLE [dbo].[{EscapeSqlId(shard)}];
END";
            await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }

    private async Task<List<Guid>> GetAllConversationIdsFromTablesAsync(string tablePrefix, CancellationToken cancellationToken = default)
    {
        var likePattern = tablePrefix.Replace("_", "\\_") + "%";
        var sql = $@"
SELECT t.name
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = N'dbo' AND t.name LIKE N'{EscapeSqlLiteral(likePattern)}' ESCAPE N'\'";
        var names = new List<string>();
        var conn = _db.Database.GetDbConnection();
        await _db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd!.CommandText = sql;
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
                names.Add(r.GetString(0));
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }

        return names
            .Where(n => n.Length > tablePrefix.Length)
            .Select(n => ParseSuffixAsGuid(n[tablePrefix.Length..]))
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .Distinct()
            .ToList();
    }

    private static Guid? ParseSuffixAsGuid(string suffix)
    {
        if (suffix.Length != 32) return null;
        try
        {
            return Guid.ParseExact(suffix, "N");
        }
        catch
        {
            return null;
        }
    }

    private async Task SyncShardToMainAsync(string mainTable, string shardTable, CancellationToken cancellationToken = default)
    {
        var mainCols = await GetColumnDefinitionsAsync(mainTable, cancellationToken);
        var shardCols = await GetColumnDefinitionsAsync(shardTable, cancellationToken);
        foreach (var kv in mainCols)
        {
            var col = kv.Key;
            var def = kv.Value;
            if (!shardCols.TryGetValue(col, out var shardDef))
            {
                var addSql = $"ALTER TABLE [dbo].[{EscapeSqlId(shardTable)}] ADD [{EscapeSqlId(col)}] {def};";
                await _db.Database.ExecuteSqlRawAsync(addSql, cancellationToken);
            }
            else if (!string.Equals(def, shardDef, StringComparison.OrdinalIgnoreCase))
            {
                var alterSql = $"ALTER TABLE [dbo].[{EscapeSqlId(shardTable)}] ALTER COLUMN [{EscapeSqlId(col)}] {def};";
                await _db.Database.ExecuteSqlRawAsync(alterSql, cancellationToken);
            }
        }
    }

    private async Task<Dictionary<string, string>> GetColumnDefinitionsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT c.name, t.name AS type_name, c.max_length, c.precision, c.scale, c.is_nullable
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
JOIN sys.tables tb ON c.object_id = tb.object_id
JOIN sys.schemas s ON tb.schema_id = s.schema_id
WHERE s.name = N'dbo' AND tb.name = N'{EscapeSqlLiteral(tableName)}' AND c.name <> N'Id'";
        var cols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var conn = _db.Database.GetDbConnection();
        await _db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd!.CommandText = sql;
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                var name = r.GetString(0);
                var typeName = r.GetString(1);
                var maxLen = r.GetInt16(2);
                var prec = r.GetByte(3);
                var scale = r.GetByte(4);
                var nullable = r.GetBoolean(5);
                var def = SqlTypeDefinition(typeName, maxLen, prec, scale, nullable);
                cols[name] = def;
            }
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }

        return cols;
    }

    private static string SqlTypeDefinition(string typeName, short maxLength, byte precision, byte scale, bool nullable)
    {
        var n = nullable ? " NULL" : " NOT NULL";
        return typeName.ToUpperInvariant() switch
        {
            "NVARCHAR" => $"NVARCHAR({(maxLength == -1 ? "MAX" : maxLength / 2)}){n}",
            "VARCHAR" => $"VARCHAR({(maxLength == -1 ? "MAX" : maxLength)}){n}",
            "NCHAR" => $"NCHAR({maxLength / 2}){n}",
            "CHAR" => $"CHAR({maxLength}){n}",
            "DECIMAL" or "NUMERIC" => $"DECIMAL({precision},{scale}){n}",
            "UNIQUEIDENTIFIER" => "UNIQUEIDENTIFIER" + n,
            "DATETIME2" => "DATETIME2(7)" + n,
            "DATETIME" => "DATETIME" + n,
            "BIT" => "BIT" + n,
            "INT" => "INT" + n,
            "BIGINT" => "BIGINT" + n,
            _ => $"{typeName}{n}"
        };
    }

    private static string EscapeSqlId(string id) => id.Replace("]", "]]");
    private static string EscapeSqlLiteral(string s) => s.Replace("'", "''");
}
