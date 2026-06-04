using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using WeiDin.Core.Enums;
using WeiDin.Core.Interfaces;
using WeiDin.Infrastructure.Repositories;

using KnowledgeEntity = WeiDin.Core.Entities.Knowledge;

namespace WeiDin.Application.Services.Knowledge;

/// <summary>
/// 文档入库服务 — 负责文件上传入队和状态查询
/// </summary>
public class DocumentIngestionService : IDocumentIngestionService
{
    private readonly Channel<(KnowledgeEntity Knowledge, string FilePath)> _channel;
    private readonly KnowledgeRepository _knowledgeRepo;
    private readonly KnowledgeChunkRepository _chunkRepo;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        Channel<(KnowledgeEntity, string)> channel,
        KnowledgeRepository knowledgeRepo,
        KnowledgeChunkRepository chunkRepo,
        ILogger<DocumentIngestionService> logger)
    {
        _channel = channel;
        _knowledgeRepo = knowledgeRepo;
        _chunkRepo = chunkRepo;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(KnowledgeEntity knowledge, string filePath, CancellationToken cancellationToken = default)
    {
        // 1. 保存 Knowledge 记录到数据库
        await _knowledgeRepo.AddAsync(knowledge);

        // 2. 入队
        await _channel.Writer.WriteAsync((knowledge, filePath), cancellationToken);

        _logger.LogInformation("文档已入队: {KnowledgeId}, 文件: {FileName}", knowledge.Id, knowledge.FileName);
    }

    /// <inheritdoc />
    public async Task<(ParseStatus Status, string? ErrorMessage, int ChunkCount)> GetStatusAsync(Guid knowledgeId, CancellationToken cancellationToken = default)
    {
        var knowledge = await _knowledgeRepo.GetByIdAsync(knowledgeId);
        if (knowledge == null)
        {
            return (ParseStatus.Failed, "知识条目不存在", 0);
        }

        var chunks = await _chunkRepo.GetByKnowledgeIdAsync(knowledgeId);
        return (knowledge.ParseStatus, knowledge.ErrorMessage, chunks.Count);
    }
}
