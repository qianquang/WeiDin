using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Volo.Abp.AspNetCore.Mvc;

namespace WeiDin.API.Controllers;

[Route("api/v1/[controller]")]
[Authorize]
public class FilesController : AbpControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FilesController> _logger;

    public FilesController(IWebHostEnvironment environment, IConfiguration configuration, ILogger<FilesController> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<FileUploadResult>> UploadFile(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("没有选择文件");

            // 检查文件大小（限制为10MB）
            if (file.Length > 10 * 1024 * 1024)
                return BadRequest("文件大小不能超过10MB");

            // 检查文件类型
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".avi", ".mov", ".pdf", ".doc", ".docx", ".txt" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest("不支持的文件类型");

            // 生成唯一文件名
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            
            // 确保上传目录存在
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            var filePath = Path.Combine(uploadsPath, fileName);

            // 保存文件
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 生成访问URL
            var baseUrl = _configuration["BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
            var fileUrl = $"{baseUrl}/uploads/{fileName}";

            _logger.LogInformation("文件上传成功: {FileName}, 大小: {FileSize} bytes", fileName, file.Length);

            return Ok(new FileUploadResult
            {
                FileName = file.FileName,
                FilePath = filePath,
                FileUrl = fileUrl,
                FileSize = file.Length,
                FileType = file.ContentType,
                UploadedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文件上传时发生错误");
            return StatusCode(500, "文件上传失败");
        }
    }

    [HttpPost("upload-multiple")]
    public async Task<ActionResult<IEnumerable<FileUploadResult>>> UploadFiles(IFormFileCollection files)
    {
        try
        {
            if (files == null || files.Count == 0)
                return BadRequest("没有选择文件");

            var results = new List<FileUploadResult>();

            foreach (var file in files)
            {
                if (file.Length == 0)
                    continue;

                // 检查文件大小
                if (file.Length > 10 * 1024 * 1024)
                {
                    results.Add(new FileUploadResult
                    {
                        FileName = file.FileName,
                        Error = "文件大小不能超过10MB"
                    });
                    continue;
                }

                // 检查文件类型
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".avi", ".mov", ".pdf", ".doc", ".docx", ".txt" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    results.Add(new FileUploadResult
                    {
                        FileName = file.FileName,
                        Error = "不支持的文件类型"
                    });
                    continue;
                }

                // 生成唯一文件名
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
                
                if (!Directory.Exists(uploadsPath))
                    Directory.CreateDirectory(uploadsPath);

                var filePath = Path.Combine(uploadsPath, fileName);

                // 保存文件
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 生成访问URL
                var baseUrl = _configuration["BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
                var fileUrl = $"{baseUrl}/uploads/{fileName}";

                results.Add(new FileUploadResult
                {
                    FileName = file.FileName,
                    FilePath = filePath,
                    FileUrl = fileUrl,
                    FileSize = file.Length,
                    FileType = file.ContentType,
                    UploadedAt = DateTime.UtcNow
                });
            }

            _logger.LogInformation("批量文件上传完成，成功: {SuccessCount}, 失败: {FailCount}", 
                results.Count(r => string.IsNullOrEmpty(r.Error)), 
                results.Count(r => !string.IsNullOrEmpty(r.Error)));

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量文件上传时发生错误");
            return StatusCode(500, "文件上传失败");
        }
    }

    [HttpGet("{fileName}")]
    public async Task<IActionResult> GetFile(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_environment.WebRootPath, "uploads", fileName);
            
            if (!System.IO.File.Exists(filePath))
                return NotFound("文件不存在");

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var contentType = GetContentType(fileName);

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取文件时发生错误，文件名: {FileName}", fileName);
            return StatusCode(500, "获取文件失败");
        }
    }

    [HttpDelete("{fileName}")]
    public Task<ActionResult> DeleteFile(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_environment.WebRootPath, "uploads", fileName);
            
            if (!System.IO.File.Exists(filePath))
                return Task.FromResult<ActionResult>(NotFound("文件不存在"));

            System.IO.File.Delete(filePath);
            _logger.LogInformation("文件删除成功: {FileName}", fileName);

            return Task.FromResult<ActionResult>(NoContent());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除文件时发生错误，文件名: {FileName}", fileName);
            return Task.FromResult<ActionResult>(StatusCode(500, "删除文件失败"));
        }
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}

public class FileUploadResult
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string? Error { get; set; }
}

