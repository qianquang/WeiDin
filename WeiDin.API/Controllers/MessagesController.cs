using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;

namespace WeiDin.API.Controllers;

/// <summary>
/// 消息 API。所有操作均携带 RelationId，用于定位分表；发送人 id 来自 JWT。
/// </summary>
[Route("api/v1/[controller]")]
[Authorize]
public class MessagesController : AbpControllerBase
{
    private readonly IMessageService _messageService;

    public MessagesController(IMessageService messageService)
    {
        _messageService = messageService;
    }

    /// <summary>按 RelationId + 消息 Id 查询单条消息。</summary>
    [HttpGet("relation/{relationId:guid}/{id:guid}")]
    public async Task<ActionResult<MessageDto>> GetMessage(Guid relationId, Guid id)
    {
        var message = await _messageService.GetByIdAsync(relationId, id);
        if (message == null)
            return NotFound("消息不存在");

        return Ok(message);
    }

    /// <summary>按 RelationId 分页查询消息列表。</summary>
    [HttpGet("relation/{relationId:guid}")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> GetByRelationId(Guid relationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var messages = await _messageService.GetByRelationIdAsync(relationId, page, pageSize);
        return Ok(messages);
    }

    /// <summary>发送消息。Body：RelationId + 消息内容 + 消息类型；发送人从 JWT 获取。</summary>
    [HttpPost]
    public async Task<ActionResult<MessageDto>> SendMessage([FromBody] CreateMessageDto createMessageDto)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var senderId))
            return Unauthorized("无效的用户身份");

        var message = await _messageService.SendMessageAsync(createMessageDto, senderId);
        return CreatedAtAction(
            nameof(GetMessage),
            new { relationId = message.RelationId, id = message.Id },
            message);
    }

    /// <summary>删除消息（仅发送者可删）。</summary>
    [HttpDelete("relation/{relationId:guid}/{id:guid}")]
    public async Task<ActionResult> DeleteMessage(Guid relationId, Guid id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("无效的用户身份");

        var result = await _messageService.DeleteMessageAsync(relationId, id, userId);
        if (!result)
            return NotFound("消息不存在或无权限删除");

        return NoContent();
    }

    /// <summary>更新消息状态。</summary>
    [HttpPut("relation/{relationId:guid}/{id:guid}/status")]
    public async Task<ActionResult> UpdateMessageStatus(Guid relationId, Guid id, [FromBody] UpdateMessageStatusDto updateDto)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("无效的用户身份");

        var result = await _messageService.UpdateMessageStatusAsync(relationId, id, userId, updateDto);
        if (!result)
            return BadRequest("更新消息状态失败");

        return Ok("状态更新成功");
    }

    /// <summary>标记已读。</summary>
    [HttpPost("relation/{relationId:guid}/{id:guid}/read")]
    public async Task<ActionResult> MarkAsRead(Guid relationId, Guid id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("无效的用户身份");

        var result = await _messageService.MarkAsReadAsync(relationId, id, userId);
        if (!result)
            return BadRequest("标记已读失败");

        return Ok("已标记为已读");
    }

    /// <summary>标记已送达。</summary>
    [HttpPost("relation/{relationId:guid}/{id:guid}/delivered")]
    public async Task<ActionResult> MarkAsDelivered(Guid relationId, Guid id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("无效的用户身份");

        var result = await _messageService.MarkAsDeliveredAsync(relationId, id, userId);
        if (!result)
            return BadRequest("标记已送达失败");

        return Ok("已标记为已送达");
    }

    /// <summary>在指定 RelationId 下搜索消息。</summary>
    [HttpGet("relation/{relationId:guid}/search")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> SearchByRelation(Guid relationId, [FromQuery] string keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("无效的用户身份");

        var messages = await _messageService.SearchByRelationAsync(relationId, userId, keyword, page, pageSize);
        return Ok(messages);
    }
}
