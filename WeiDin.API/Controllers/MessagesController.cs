using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;

namespace WeiDin.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(IMessageService messageService, ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MessageDto>> GetMessage(Guid id)
    {
        try
        {
            var message = await _messageService.GetByIdAsync(id);
            if (message == null)
                return NotFound("消息不存在");

            return Ok(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取消息时发生错误，消息ID: {MessageId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> GetUserMessages(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var messages = await _messageService.GetByUserIdAsync(userId, page, pageSize);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户消息时发生错误，用户ID: {UserId}", userId);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpGet("group/{groupId}")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> GetGroupMessages(Guid groupId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var messages = await _messageService.GetByGroupIdAsync(groupId, page, pageSize);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取群组消息时发生错误，群组ID: {GroupId}", groupId);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpGet("conversation/{userId1}/{userId2}")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> GetConversation(Guid userId1, Guid userId2, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var messages = await _messageService.GetConversationAsync(userId1, userId2, page, pageSize);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取对话消息时发生错误，用户1: {UserId1}, 用户2: {UserId2}", userId1, userId2);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost]
    public async Task<ActionResult<MessageDto>> SendMessage(CreateMessageDto createMessageDto)
    {
        try
        {
            // 从JWT token中获取用户ID
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var senderId))
                return Unauthorized("无效的用户身份");

            var message = await _messageService.SendMessageAsync(createMessageDto, senderId);
            return CreatedAtAction(nameof(GetMessage), new { id = message.Id }, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送消息时发生错误");
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteMessage(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var result = await _messageService.DeleteMessageAsync(id, userId);
            if (!result)
                return NotFound("消息不存在或无权限删除");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除消息时发生错误，消息ID: {MessageId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult> UpdateMessageStatus(Guid id, UpdateMessageStatusDto updateDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var result = await _messageService.UpdateMessageStatusAsync(id, userId, updateDto);
            if (!result)
                return BadRequest("更新消息状态失败");

            return Ok("状态更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新消息状态时发生错误，消息ID: {MessageId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost("{id}/read")]
    public async Task<ActionResult> MarkAsRead(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var result = await _messageService.MarkAsReadAsync(id, userId);
            if (!result)
                return BadRequest("标记已读失败");

            return Ok("已标记为已读");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "标记消息已读时发生错误，消息ID: {MessageId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost("{id}/delivered")]
    public async Task<ActionResult> MarkAsDelivered(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var result = await _messageService.MarkAsDeliveredAsync(id, userId);
            if (!result)
                return BadRequest("标记已送达失败");

            return Ok("已标记为已送达");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "标记消息已送达时发生错误，消息ID: {MessageId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> SearchMessages([FromQuery] string keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var messages = await _messageService.SearchMessagesAsync(userId, keyword, page, pageSize);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索消息时发生错误，关键词: {Keyword}", keyword);
            return StatusCode(500, "服务器内部错误");
        }
    }
}
