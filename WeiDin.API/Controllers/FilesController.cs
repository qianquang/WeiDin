using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Volo.Abp.AspNetCore.Mvc;
using WeiDin.API.Hubs;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;

namespace WeiDin.API.Controllers;

[Route("api/v1/[controller]")]
[Authorize]
public class FilesController : AbpControllerBase
{
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".avi", ".mov", ".pdf", ".doc", ".docx", ".txt"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IMessageService _messageService;
    private readonly IFriendshipService _friendshipService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IMessageService messageService,
        IFriendshipService friendshipService,
        IHubContext<ChatHub> hubContext,
        ILogger<FilesController> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _messageService = messageService;
        _friendshipService = friendshipService;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<FileUploadResult>> UploadFile(IFormFile file)
    {
        try
        {
            var stored = await SaveFileAsync(file);

            _logger.LogInformation("文件上传成功: {FileName}, 大小: {FileSize} bytes", stored.StoredFileName, stored.FileSize);

            return Ok(new FileUploadResult
            {
                FileName = stored.OriginalFileName,
                FilePath = stored.RelativePath,
                FileUrl = stored.FileUrl,
                FileSize = stored.FileSize,
                FileType = stored.FileType,
                UploadedAt = DateTime.UtcNow
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
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

                try
                {
                    var stored = await SaveFileAsync(file);
                    results.Add(new FileUploadResult
                    {
                        FileName = stored.OriginalFileName,
                        FilePath = stored.RelativePath,
                        FileUrl = stored.FileUrl,
                        FileSize = stored.FileSize,
                        FileType = stored.FileType,
                        UploadedAt = DateTime.UtcNow
                    });
                }
                catch (InvalidOperationException ex)
                {
                    results.Add(new FileUploadResult
                    {
                        FileName = file.FileName,
                        Error = ex.Message
                    });
                }
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

    [HttpPost("upload-and-send")]
    public async Task<ActionResult<MessageDto>> UploadAndSendFile([FromForm] UploadAndSendFileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized("无效的用户身份");

            if (request.RelationId == Guid.Empty)
                return BadRequest("relationId 不能为空");
            if (request.File == null)
                return BadRequest("没有选择文件");

            var stored = await SaveFileAsync(request.File);

            var createMessageDto = new CreateMessageDto
            {
                RelationId = request.RelationId,
                MessageType = ResolveMessageType(stored.StoredFileName, request.MessageType),
                Content = string.IsNullOrWhiteSpace(request.Content) ? stored.FileUrl : request.Content.Trim(),
                Attachments = new List<CreateMessageAttachmentDto>
                {
                    new()
                    {
                        FileName = stored.OriginalFileName,
                        FilePath = stored.RelativePath,
                        FileType = stored.FileType,
                        FileSize = stored.FileSize
                    }
                }
            };

            var message = await _messageService.SendMessageAsync(createMessageDto, userId.Value);
            await NotifyMessageAsync(message, userId.Value);

            return CreatedAtAction(
                nameof(MessagesController.GetMessage),
                "Messages",
                new { relationId = message.RelationId, id = message.Id },
                message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传并发送文件时发生错误");
            return StatusCode(500, "发送文件失败");
        }
    }

    [HttpGet("{fileName}")]
    public async Task<IActionResult> GetFile(string fileName)
    {
        try
        {
            var safeName = Path.GetFileName(fileName);
            if (!string.Equals(fileName, safeName, StringComparison.Ordinal))
                return BadRequest("非法文件名");

            var filePath = Path.Combine(GetUploadsPath(), safeName);
            
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
            var safeName = Path.GetFileName(fileName);
            if (!string.Equals(fileName, safeName, StringComparison.Ordinal))
                return Task.FromResult<ActionResult>(BadRequest("非法文件名"));

            var filePath = Path.Combine(GetUploadsPath(), safeName);
            
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

    private async Task<StoredFileResult> SaveFileAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("没有选择文件");

        if (file.Length > MaxFileSize)
            throw new InvalidOperationException("文件大小不能超过10MB");

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("不支持的文件类型");

        var storedFileName = $"{Guid.NewGuid()}{extension.ToLowerInvariant()}";
        var uploadsPath = GetUploadsPath();
        Directory.CreateDirectory(uploadsPath);

        var filePath = Path.Combine(uploadsPath, storedFileName);
        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var baseUrl = (_configuration["BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');
        var fileUrl = $"{baseUrl}/api/v1/files/{storedFileName}";
        var relativePath = $"uploads/{storedFileName}";

        return new StoredFileResult
        {
            OriginalFileName = file.FileName,
            StoredFileName = storedFileName,
            RelativePath = relativePath,
            FileUrl = fileUrl,
            FileSize = file.Length,
            FileType = file.ContentType
        };
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId) ? userId : null;
    }

    private string ResolveMessageType(string storedFileName, string? requestedType)
    {
        if (!string.IsNullOrWhiteSpace(requestedType))
            return requestedType.Trim();

        var extension = Path.GetExtension(storedFileName).ToLowerInvariant();
        if (extension is ".jpg" or ".jpeg" or ".png" or ".gif")
            return "Image";
        if (extension is ".mp4" or ".avi" or ".mov")
            return "Video";
        return "File";
    }

    private string GetUploadsPath()
    {
        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");

        return Path.Combine(webRoot, "uploads");
    }

    private async Task NotifyMessageAsync(MessageDto message, Guid senderId)
    {
        try
        {
            Guid? receiverId = message.ReceiverId;

            if (!receiverId.HasValue && !message.GroupId.HasValue)
            {
                var friendship = await _friendshipService.GetByConversationIdAsync(message.RelationId, senderId);
                if (friendship != null)
                {
                    receiverId = friendship.UserId == senderId ? friendship.FriendId : friendship.UserId;
                    message.ReceiverId = receiverId;
                    message.ReceiverName = friendship.UserId == senderId ? friendship.FriendName : friendship.UserName;
                }
            }

            if (receiverId.HasValue)
            {
                await _hubContext.Clients.Group($"user_{receiverId.Value}").SendAsync("ReceiveMessage", message);
            }

            if (message.GroupId.HasValue)
            {
                await _hubContext.Clients.Group($"group_{message.GroupId.Value}").SendAsync("ReceiveGroupMessage", message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "发送文件消息的 SignalR 通知失败，但消息已保存");
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

    private sealed class StoredFileResult
    {
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileType { get; set; } = string.Empty;
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

public class UploadAndSendFileRequest
{
    public Guid RelationId { get; set; }
    public string? MessageType { get; set; }
    public string? Content { get; set; }
    public IFormFile? File { get; set; }
}

