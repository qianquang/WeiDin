using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Mvc;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.API.Hubs;

namespace WeiDin.API.Controllers;

/// <summary>
/// 消息 API。所有操作均携带 RelationId，用于定位分表；发送人 id 来自 JWT。
/// </summary>
[Route("api/v1/[controller]")]
[Authorize]
public class MessagesController : AbpControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IFriendshipService _friendshipService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        IMessageService messageService,
        IFriendshipService friendshipService,
        IHubContext<ChatHub> hubContext,
        ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _friendshipService = friendshipService;
        _hubContext = hubContext;
        _logger = logger;
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

        // 发送 SignalR 通知给接收方
        try
        {
            Guid? receiverId = message.ReceiverId;

            // 如果 ReceiverId 为空且不是群组消息，通过 conversationId 查询 Friendship 获取接收方ID
            if (!receiverId.HasValue && !message.GroupId.HasValue)
            {
                var friendship = await _friendshipService.GetByConversationIdAsync(message.RelationId, senderId);
                if (friendship != null)
                {
                    receiverId = friendship.UserId == senderId ? friendship.FriendId : friendship.UserId;
                    // 将接收方信息回填到消息对象，确保 SignalR 推送的消息包含完整信息
                    message.ReceiverId = receiverId;
                    var receiverName = friendship.UserId == senderId ? friendship.FriendName : friendship.UserName;
                    message.ReceiverName = receiverName;
                }
                else
                {
                    _logger.LogWarning("未找到 conversationId={ConversationId} 对应的好友关系，无法确定接收方", message.RelationId);
                }
            }

            // 私聊消息：发送给接收方
            if (receiverId.HasValue)
            {
                var targetGroup = $"user_{receiverId.Value}";
                await _hubContext.Clients.Group(targetGroup).SendAsync("ReceiveMessage", message);
                _logger.LogInformation("已通过 SignalR 发送消息通知给用户 {ReceiverId}", receiverId.Value);
            }

            // 群组消息：发送给群组所有成员
            if (message.GroupId.HasValue)
            {
                var groupName = $"group_{message.GroupId.Value}";
                await _hubContext.Clients.Group(groupName).SendAsync("ReceiveGroupMessage", message);
                _logger.LogInformation("已通过 SignalR 发送群组消息通知给群组 {GroupId}", message.GroupId.Value);
            }
        }
        catch (Exception ex)
        {
            // SignalR 通知失败不影响消息保存的成功响应
            _logger.LogWarning(ex, "发送 SignalR 通知失败，但消息已成功保存");
        }

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

    /// <summary>批量标记已读。</summary>
    [HttpPost("relation/{relationId:guid}/read-all")]
    public async Task<ActionResult<IEnumerable<Guid>>> MarkAllAsRead(Guid relationId)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("无效的用户身份");

        try
        {
            var markedMessageIds = await _messageService.MarkAllAsReadAsync(relationId, userId);
            _logger.LogInformation("用户 {UserId} 批量标记会话 {RelationId} 的 {Count} 条消息为已读", userId, relationId, markedMessageIds.Count);
            return Ok(markedMessageIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "用户 {UserId} 批量标记会话 {RelationId} 的消息为已读时发生错误", userId, relationId);
            throw;
        }
    }

    /// <summary>获取未读消息数量。</summary>
    [HttpGet("relation/{relationId:guid}/unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(Guid relationId)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized("无效的用户身份");

        var count = await _messageService.GetUnreadCountAsync(relationId, userId);
        return Ok(count);
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
