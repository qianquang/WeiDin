using Microsoft.EntityFrameworkCore;
using WeiDin.Core.Entities;
using WeiDin.Infrastructure.Data;

namespace WeiDin.Infrastructure.Repositories;

/// <summary>
/// 知识条目仓储
/// </summary>
public class KnowledgeRepository : Repository<Knowledge>
{
    public KnowledgeRepository(WeiDinDbContext context) : base(context)
    {
    }

    /// <summary>
    /// 根据知识库ID获取所有知识条目
    /// </summary>
    public async Task<List<Knowledge>> GetByKnowledgeBaseIdAsync(Guid knowledgeBaseId)
    {
        return await _dbSet
            .Where(k => k.KnowledgeBaseId == knowledgeBaseId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// 根据ID获取知识条目（含分块）
    /// </summary>
    public async Task<Knowledge?> GetByIdWithChunksAsync(Guid id)
    {
        return await _dbSet
            .Include(k => k.Chunks)
            .FirstOrDefaultAsync(k => k.Id == id);
    }
}
