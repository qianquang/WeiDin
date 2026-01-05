using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;

namespace WeiDin.API.Controllers;

[Route("api/v1/[controller]")]
[Authorize]
public class MessagesController : AbpControllerBase
{
    private readonly IMessageService _messageService;

    public MessagesController(IMessageService messageService)
    {
        _messageService = messageService;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MessageDto>> GetMessage(Guid id)
    {
        var message = await _messageService.GetByIdAsync(id);
        if (message == null)
            return NotFound("消息不存在");

        return Ok(message);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> GetUserMessages(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var messages = await _messageService.GetByUserIdAsync(userId, page, pageSize);
        return Ok(messages);
    }

    [HttpGet("group/{groupId}")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> GetGroupMessages(Guid groupId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var messages = await _messageService.GetByGroupIdAsync(groupId, page, pageSize);
        return Ok(messages);
    }

    [HttpGet("conversation/{userId1}/{userId2}")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> GetConversation(Guid userId1, Guid userId2, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var messages = await _messageService.GetConversationAsync(userId1, userId2, page, pageSize);
        return Ok(messages);
    }

    [HttpPost]
    public async Task<ActionResult<MessageDto>> SendMessage(CreateMessageDto createMessageDto)
    {
        // 从JWT token中获取用户ID
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var senderId))
            return Unauthorized("无效的用户身份");

        var message = await _messageService.SendMessageAsync(createMessageDto, senderId);
        return CreatedAtAction(nameof(GetMessage), new { id = message.Id }, message);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteMessage(Guid id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("无效的用户身份");

        var result = await _messageService.DeleteMessageAsync(id, userId);
        if (!result)
            return NotFound("消息不存在或无权限删除");

        return NoContent();
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult> UpdateMessageStatus(Guid id, UpdateMessageStatusDto updateDto)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("无效的用户身份");

        var result = await _messageService.UpdateMessageStatusAsync(id, userId, updateDto);
        if (!result)
            return BadRequest("更新消息状态失败");

        return Ok("状态更新成功");
    }

    [HttpPost("{id}/read")]
    public async Task<ActionResult> MarkAsRead(Guid id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("无效的用户身份");

        var result = await _messageService.MarkAsReadAsync(id, userId);
        if (!result)
            return BadRequest("标记已读失败");

        return Ok("已标记为已读");
    }

    [HttpPost("{id}/delivered")]
    public async Task<ActionResult> MarkAsDelivered(Guid id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("无效的用户身份");

        var result = await _messageService.MarkAsDeliveredAsync(id, userId);
        if (!result)
            return BadRequest("标记已送达失败");

        return Ok("已标记为已送达");
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> SearchMessages([FromQuery] string keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("无效的用户身份");

        var messages = await _messageService.SearchMessagesAsync(userId, keyword, page, pageSize);
        return Ok(messages);
    }
}
