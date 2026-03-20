using Microsoft.EntityFrameworkCore;
using WeiDin.Core.Entities;
using WeiDin.Infrastructure.Data;

namespace WeiDin.Infrastructure.Repositories;

/// <summary>
/// 知识库仓储
/// </summary>
public class KnowledgeBaseRepository : Repository<KnowledgeBase>
{
    public KnowledgeBaseRepository(WeiDinDbContext context) : base(context)
    {
    }

    /// <summary>
    /// 根据名称获取知识库
    /// </summary>
    public async Task<KnowledgeBase?> GetByNameAsync(string name)
    {
        return await _dbSet.FirstOrDefaultAsync(k => k.Name == name);
    }

    /// <summary>
    /// 获取所有启用的知识库
    /// </summary>
    public async Task<List<KnowledgeBase>> GetEnabledAsync()
    {
        return await _dbSet
            .Where(k => k.IsEnabled)
            .OrderBy(k => k.Name)
            .ToListAsync();
    }

    /// <summary>
    /// 更新知识块数量
    /// </summary>
    public async Task UpdateChunkCountAsync(Guid knowledgeBaseId)
    {
        var count = await _dbSet
            .Where(k => k.Id == knowledgeBaseId)
            .SelectMany(k => k.Chunks)
            .CountAsync();

        var knowledgeBase = await _dbSet.FindAsync(knowledgeBaseId);
        if (knowledgeBase != null)
        {
            knowledgeBase.ChunkCount = count;
            knowledgeBase.UpdatedAt = DateTime.UtcNow;
            await UpdateAsync(knowledgeBase);
        }
    }
}
