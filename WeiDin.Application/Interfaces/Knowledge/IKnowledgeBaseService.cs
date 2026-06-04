using WeiDin.Application.DTOs;
using WeiDin.Core.Entities;

namespace WeiDin.Application.Interfaces.Knowledge;

public interface IKnowledgeBaseService
{
    // 知识库 CRUD
    Task<KnowledgeBaseDto> CreateAsync(CreateKnowledgeBaseDto input);
    Task<List<KnowledgeBaseDto>> GetAllAsync();
    Task<KnowledgeBaseDto?> GetByIdAsync(Guid id);
    Task<KnowledgeBaseDto?> UpdateAsync(Guid id, UpdateKnowledgeBaseDto input);
    Task<bool> DeleteAsync(Guid id);

    // 知识块管理
    Task<KnowledgeChunkDto> AddChunkAsync(Guid knowledgeBaseId, AddChunkDto input);
    Task<List<KnowledgeChunkDto>> AddChunksBatchAsync(Guid knowledgeBaseId, AddChunksBatchDto input);
    Task<List<KnowledgeChunkDto>> GetChunksAsync(Guid knowledgeBaseId, int page = 1, int pageSize = 50);
    Task<bool> DeleteChunkAsync(Guid knowledgeBaseId, Guid chunkId);
    Task<int> ClearChunksAsync(Guid knowledgeBaseId);

    // 文档上传（文本分块入库）
    Task<List<KnowledgeChunkDto>> UploadTextAsync(Guid knowledgeBaseId, string text, UploadDocumentDto input);

    // 检索
    Task<List<KnowledgeChunkDto>> RetrieveAsync(RetrieveQueryDto input);
}
