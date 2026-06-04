using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WeiDin.Core.Enums;
using WeiDin.Core.Interfaces;
using WeiDin.Infrastructure.Repositories;

using KnowledgeEntity = WeiDin.Core.Entities.Knowledge;

namespace WeiDin.Infrastructure.Background;

/// <summary>
/// 文档处理后台服务 — 消费 Channel 队列，执行解析→分块→向量化→入库
/// </summary>
public class DocumentProcessingWorker : BackgroundService
{
    private readonly Channel<(KnowledgeEntity Knowledge, string FilePath)> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentProcessingWorker> _logger;

    public DocumentProcessingWorker(
        Channel<(KnowledgeEntity, string)> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentProcessingWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DocumentProcessingWorker 启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var (knowledge, filePath) in _channel.Reader.ReadAllAsync(stoppingToken))
                {
                    await ProcessDocumentAsync(knowledge, filePath, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DocumentProcessingWorker 循环异常");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("DocumentProcessingWorker 已停止");
    }

    private async Task ProcessDocumentAsync(KnowledgeEntity knowledge, string filePath, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var knowledgeRepo = scope.ServiceProvider.GetRequiredService<KnowledgeRepository>();
        var chunkRepo = scope.ServiceProvider.GetRequiredService<KnowledgeChunkRepository>();
        var documentParser = scope.ServiceProvider.GetRequiredService<IDocumentParser>();
        var chunkingService = scope.ServiceProvider.GetRequiredService<ITextChunkingService>();
        var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var retrievalService = scope.ServiceProvider.GetRequiredService<IRetrievalService>();

        try
        {
            _logger.LogInformation("开始处理文档: {KnowledgeId}, 文件: {FileName}", knowledge.Id, knowledge.FileName);

            // 1. 更新状态 → processing
            knowledge.ParseStatus = ParseStatus.Processing;
            await knowledgeRepo.UpdateAsync(knowledge);

            // 2. 解析文件
            string text;
            await using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                text = await documentParser.ExtractTextAsync(fileStream, knowledge.FileName, ct);
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("文件解析结果为空");
            }

            // 3. 分块
            var textChunks = chunkingService.Chunk(text, 500, 50);
            if (textChunks.Count == 0)
            {
                throw new InvalidOperationException("分块结果为空");
            }

            // 4. 创建 Chunk 实体 + 构建双向链表
            var chunks = new List<WeiDin.Core.Entities.KnowledgeChunk>();
            for (int i = 0; i < textChunks.Count; i++)
            {
                chunks.Add(new WeiDin.Core.Entities.KnowledgeChunk
                {
                    Id = Guid.NewGuid(),
                    KnowledgeId = knowledge.Id,
                    KnowledgeBaseId = knowledge.KnowledgeBaseId,
                    Content = textChunks[i],
                    ChunkIndex = i,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // 构建双向链表
            for (int i = 0; i < chunks.Count; i++)
            {
                if (i > 0) chunks[i].PreChunkId = chunks[i - 1].Id;
                if (i < chunks.Count - 1) chunks[i].NextChunkId = chunks[i + 1].Id;
            }

            // 5. 分批向量化 + 写入
            const int batchSize = 10;
            for (int i = 0; i < chunks.Count; i += batchSize)
            {
                var batch = chunks.Skip(i).Take(batchSize).ToList();
                var texts = batch.Select(c => c.Content).ToList();
                var vectors = await embeddingService.EmbedBatchAsync(texts, ct);

                var chunkVectorPairs = batch.Zip(vectors, (chunk, vector) => (chunk, vector)).ToList();
                await retrievalService.AddChunksAsync(chunkVectorPairs, ct);

                // 写入 SQL Server（结构化数据）
                await chunkRepo.AddRangeAsync(batch);

                _logger.LogInformation("文档 {KnowledgeId} 已处理 {Current}/{Total} 块",
                    knowledge.Id, Math.Min(i + batchSize, chunks.Count), chunks.Count);
            }

            // 6. 更新状态 → completed
            knowledge.ParseStatus = ParseStatus.Completed;
            await knowledgeRepo.UpdateAsync(knowledge);

            _logger.LogInformation("文档处理完成: {KnowledgeId}, 共 {Count} 块", knowledge.Id, chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文档处理失败: {KnowledgeId}", knowledge.Id);
            knowledge.ParseStatus = ParseStatus.Failed;
            knowledge.ErrorMessage = ex.Message;
            await knowledgeRepo.UpdateAsync(knowledge);
        }
    }
}
