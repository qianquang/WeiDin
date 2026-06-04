using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces.Knowledge;
using WeiDin.Core.Entities;
using WeiDin.Core.Enums;
using WeiDin.Core.Interfaces;
using WeiDin.Infrastructure.Repositories;

namespace WeiDin.API.Controllers;

/// <summary>
/// 知识库管理控制器
/// </summary>
[Route("api/v1/[controller]")]
[Authorize]
public class KnowledgeController : AbpControllerBase
{
    private readonly IKnowledgeBaseService _kbService;
    private readonly IDocumentIngestionService _ingestionService;
    private readonly KnowledgeBaseRepository _kbRepository;
    private readonly ILogger<KnowledgeController> _logger;

    // 允许的文件类型
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".pdf", ".docx"
    };

    public KnowledgeController(
        IKnowledgeBaseService kbService,
        IDocumentIngestionService ingestionService,
        KnowledgeBaseRepository kbRepository,
        ILogger<KnowledgeController> logger)
    {
        _kbService = kbService;
        _ingestionService = ingestionService;
        _kbRepository = kbRepository;
        _logger = logger;
    }

    // ========== 知识库 CRUD ==========

    /// <summary>
    /// 创建知识库
    /// </summary>
    [HttpPost("bases")]
    public async Task<ActionResult<KnowledgeBaseDto>> CreateKnowledgeBase([FromBody] CreateKnowledgeBaseDto input)
    {
        var result = await _kbService.CreateAsync(input);
        return Ok(result);
    }

    /// <summary>
    /// 获取所有知识库
    /// </summary>
    [HttpGet("bases")]
    public async Task<ActionResult<List<KnowledgeBaseDto>>> GetAllKnowledgeBases()
    {
        var result = await _kbService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>
    /// 获取知识库详情
    /// </summary>
    [HttpGet("bases/{id}")]
    public async Task<ActionResult<KnowledgeBaseDto>> GetKnowledgeBase(Guid id)
    {
        var result = await _kbService.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// 更新知识库
    /// </summary>
    [HttpPut("bases/{id}")]
    public async Task<ActionResult<KnowledgeBaseDto>> UpdateKnowledgeBase(Guid id, [FromBody] UpdateKnowledgeBaseDto input)
    {
        var result = await _kbService.UpdateAsync(id, input);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// 删除知识库
    /// </summary>
    [HttpDelete("bases/{id}")]
    public async Task<IActionResult> DeleteKnowledgeBase(Guid id)
    {
        var success = await _kbService.DeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }

    // ========== 文档上传（异步入库） ==========

    /// <summary>
    /// 上传文档到知识库（异步处理）
    /// 文件会被解析→分块→向量化→入库，处理完成后通过状态接口查询结果
    /// </summary>
    [HttpPost("bases/{knowledgeBaseId}/documents/upload")]
    public async Task<ActionResult<DocumentIngestionStatusDto>> UploadDocument(
        Guid knowledgeBaseId,
        IFormFile file,
        [FromQuery] KnowledgeChunkType type = KnowledgeChunkType.Custom)
    {
        if (file == null || file.Length == 0)
            return BadRequest("请选择要上传的文件");

        // 检查文件类型
        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            return BadRequest($"不支持的文件类型: {ext}，支持: {string.Join(", ", AllowedExtensions)}");

        // 检查知识库是否存在
        var kb = await _kbRepository.GetByIdAsync(knowledgeBaseId);
        if (kb == null) return NotFound("知识库不存在");

        // 保存文件
        var uploadDir = Path.Combine("wwwroot", "uploads", "knowledge");
        Directory.CreateDirectory(uploadDir);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // 创建 Knowledge 记录
        var knowledge = new Knowledge
        {
            Id = Guid.NewGuid(),
            KnowledgeBaseId = knowledgeBaseId,
            FileName = file.FileName,
            FilePath = filePath,
            ParseStatus = ParseStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        // 入队
        await _ingestionService.EnqueueAsync(knowledge, filePath);

        _logger.LogInformation("文档已上传并入队: {KnowledgeId}, 文件: {FileName}", knowledge.Id, file.FileName);

        return Ok(new DocumentIngestionStatusDto
        {
            KnowledgeId = knowledge.Id,
            FileName = knowledge.FileName,
            ParseStatus = ParseStatus.Pending,
            ChunkCount = 0
        });
    }

    /// <summary>
    /// 查询文档处理状态
    /// </summary>
    [HttpGet("documents/{knowledgeId}/status")]
    public async Task<ActionResult<DocumentIngestionStatusDto>> GetDocumentStatus(Guid knowledgeId)
    {
        var (status, errorMessage, chunkCount) = await _ingestionService.GetStatusAsync(knowledgeId);

        return Ok(new DocumentIngestionStatusDto
        {
            KnowledgeId = knowledgeId,
            ParseStatus = status,
            ErrorMessage = errorMessage,
            ChunkCount = chunkCount
        });
    }

    // ========== 知识块管理 ==========

    /// <summary>
    /// 添加单个知识块
    /// </summary>
    [HttpPost("bases/{knowledgeBaseId}/chunks")]
    public async Task<ActionResult<KnowledgeChunkDto>> AddChunk(Guid knowledgeBaseId, [FromBody] AddChunkDto input)
    {
        var result = await _kbService.AddChunkAsync(knowledgeBaseId, input);
        return Ok(result);
    }

    /// <summary>
    /// 批量添加知识块
    /// </summary>
    [HttpPost("bases/{knowledgeBaseId}/chunks/batch")]
    public async Task<ActionResult<List<KnowledgeChunkDto>>> AddChunksBatch(Guid knowledgeBaseId, [FromBody] AddChunksBatchDto input)
    {
        var result = await _kbService.AddChunksBatchAsync(knowledgeBaseId, input);
        return Ok(result);
    }

    /// <summary>
    /// 获取知识块列表
    /// </summary>
    [HttpGet("bases/{knowledgeBaseId}/chunks")]
    public async Task<ActionResult<List<KnowledgeChunkDto>>> GetChunks(
        Guid knowledgeBaseId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _kbService.GetChunksAsync(knowledgeBaseId, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// 删除知识块
    /// </summary>
    [HttpDelete("bases/{knowledgeBaseId}/chunks/{chunkId}")]
    public async Task<IActionResult> DeleteChunk(Guid knowledgeBaseId, Guid chunkId)
    {
        var success = await _kbService.DeleteChunkAsync(knowledgeBaseId, chunkId);
        if (!success) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// 清空知识库的所有知识块
    /// </summary>
    [HttpDelete("bases/{knowledgeBaseId}/chunks")]
    public async Task<ActionResult<int>> ClearChunks(Guid knowledgeBaseId)
    {
        var count = await _kbService.ClearChunksAsync(knowledgeBaseId);
        return Ok(count);
    }
}
