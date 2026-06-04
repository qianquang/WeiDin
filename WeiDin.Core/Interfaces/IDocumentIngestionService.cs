using WeiDin.Core.Entities;
using WeiDin.Core.Enums;

namespace WeiDin.Core.Interfaces;

/// <summary>
/// 文档入库服务接口 — 负责文件上传入队和状态查询
/// </summary>
public interface IDocumentIngestionService
{
    /// <summary>
    /// 将文档入队等待异步处理
    /// </summary>
    /// <param name="knowledge">已创建的 Knowledge 记录</param>
    /// <param name="filePath">文件存储路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task EnqueueAsync(Knowledge knowledge, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询知识条目的处理状态
    /// </summary>
    /// <param name="knowledgeId">知识条目ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(ParseStatus Status, string? ErrorMessage, int ChunkCount)> GetStatusAsync(Guid knowledgeId, CancellationToken cancellationToken = default);
}
