using Microsoft.EntityFrameworkCore;
using WeiDin.Core.Entities;
using WeiDin.Core.Enums;
using WeiDin.Infrastructure.Data;

namespace WeiDin.Infrastructure.Repositories;

/// <summary>
/// 知识块仓储
/// </summary>
public class KnowledgeChunkRepository : Repository<KnowledgeChunk>
{
    public KnowledgeChunkRepository(WeiDinDbContext context) : base(context)
    {
    }

    /// <summary>
    /// 根据知识库ID获取所有知识块
    /// </summary>
    public async Task<List<KnowledgeChunk>> GetByKnowledgeBaseIdAsync(Guid knowledgeBaseId)
    {
        return await _dbSet
            .Where(c => c.KnowledgeBaseId == knowledgeBaseId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync();
    }

    /// <summary>
    /// 根据RelationId获取知识块
    /// </summary>
    public async Task<List<KnowledgeChunk>> GetByRelationIdAsync(Guid relationId)
    {
        return await _dbSet
            .Where(c => c.RelationId == relationId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// 批量获取知识块
    /// </summary>
    public async Task<List<KnowledgeChunk>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        return await _dbSet
            .Where(c => idList.Contains(c.Id))
            .ToListAsync();
    }

    /// <summary>
    /// 获取知识库的最新N条聊天记录
    /// </summary>
    public async Task<List<KnowledgeChunk>> GetRecentChatMessagesAsync(Guid knowledgeBaseId, int count = 100)
    {
        return await _dbSet
            .Where(c => c.KnowledgeBaseId == knowledgeBaseId && c.Type == KnowledgeChunkType.ChatMessage)
            .OrderByDescending(c => c.CreatedAt)
            .Take(count)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }
}
