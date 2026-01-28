using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Volo.Abp.AspNetCore.Mvc;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.API.Hubs;

namespace WeiDin.API.Controllers;

[Route("api/v1/[controller]")]
[Authorize]
public class FriendshipsController : AbpControllerBase
{
    private readonly IFriendshipService _friendshipService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<FriendshipsController> _logger;

    public FriendshipsController(
        IFriendshipService friendshipService,
        IHubContext<ChatHub> hubContext,
        ILogger<FriendshipsController> logger)
    {
        _friendshipService = friendshipService;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FriendshipDto>>> GetFriendships()
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var friendships = await _friendshipService.GetByUserIdAsync(userId);
            return Ok(friendships);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取好友列表时发生错误");
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FriendshipDto>> GetFriendship(Guid id)
    {
        try
        {
            var friendship = await _friendshipService.GetByIdAsync(id);
            if (friendship == null)
                return NotFound("好友关系不存在");

            return Ok(friendship);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取好友关系时发生错误，ID: {FriendshipId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost]
    public async Task<ActionResult<FriendshipDto>> AddFriend([FromBody] CreateFriendshipDto createDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var friendship = await _friendshipService.AddFriendAsync(createDto, userId);
            
            // 发送 SignalR 通知给被申请的用户
            try
            {
                await _hubContext.Clients.Group($"user_{createDto.FriendId}")
                    .SendAsync("FriendRequestReceived", friendship);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发送好友申请通知失败，FriendId: {FriendId}", createDto.FriendId);
            }
            
            return CreatedAtAction(nameof(GetFriendship), new { id = friendship.Id }, friendship);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            // 记录详细的数据库错误信息
            var innerException = dbEx.InnerException?.Message ?? dbEx.Message;
            _logger.LogError(dbEx, "数据库更新错误: {InnerException}", innerException);
            
            // 检查是否是唯一索引冲突
            // if (innerException.Contains("UNIQUE") || innerException.Contains("duplicate key") || innerException.Contains("IX_Friendships"))
            // {
            //     return BadRequest("已经存在相同的好友关系或申请，请检查是否已发送过申请");
            // }
            
            // 检查是否是外键约束
            if (innerException.Contains("FOREIGN KEY") || innerException.Contains("REFERENCES") || innerException.Contains("FK_Friendships"))
            {
                return BadRequest("用户不存在，请检查用户ID是否正确");
            }
            
            // 检查是否是主键错误
            if (innerException.Contains("PRIMARY KEY") || innerException.Contains("Id"))
            {
                return BadRequest("数据ID生成失败，请重试");
            }
            
            return StatusCode(500, $"数据库错误: {innerException}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送好友申请时发生错误: {Exception}", ex.ToString());
            return StatusCode(500, $"服务器内部错误: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> RemoveFriend(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var result = await _friendshipService.RemoveFriendAsync(id, userId);
            if (!result)
                return NotFound("好友关系不存在");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除好友时发生错误，ID: {FriendshipId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost("{id}/accept")]
    public async Task<ActionResult<FriendshipDto>> AcceptFriendRequest(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var friendship = await _friendshipService.AcceptFriendRequestAsync(id, userId);
            
            // 发送 SignalR 通知给申请发起者
            try
            {
                await _hubContext.Clients.Group($"user_{friendship.UserId}")
                    .SendAsync("FriendRequestAccepted", new
                    {
                        FriendshipId = friendship.Id,
                        FriendName = friendship.FriendName
                    });
                
                // 通知双方新好友已添加
                await _hubContext.Clients.Group($"user_{friendship.UserId}")
                    .SendAsync("NewFriendAdded", friendship);
                await _hubContext.Clients.Group($"user_{friendship.FriendId}")
                    .SendAsync("NewFriendAdded", friendship);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发送接受申请通知失败，FriendshipId: {FriendshipId}", id);
            }
            
            return Ok(friendship);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接受好友申请时发生错误，ID: {FriendshipId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost("{id}/reject")]
    public async Task<ActionResult> RejectFriendRequest(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            // 先获取申请信息（用于通知）
            var request = await _friendshipService.GetByIdAsync(id);
            
            var result = await _friendshipService.RejectFriendRequestAsync(id, userId);
            if (!result)
                return NotFound("好友申请不存在或已被处理");

            // 发送 SignalR 通知给申请发起者
            if (request != null)
            {
                try
                {
                    await _hubContext.Clients.Group($"user_{request.UserId}")
                        .SendAsync("FriendRequestRejected", new
                        {
                            FriendshipId = id,
                            FriendName = request.FriendName
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "发送拒绝申请通知失败，FriendshipId: {FriendshipId}", id);
                }
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "拒绝好友申请时发生错误，ID: {FriendshipId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<FriendshipDto>>> GetPendingRequests()
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var requests = await _friendshipService.GetPendingRequestsAsync(userId);
            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取待处理好友申请时发生错误");
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpGet("sent")]
    public async Task<ActionResult<IEnumerable<FriendshipDto>>> GetSentRequests()
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var requests = await _friendshipService.GetSentRequestsAsync(userId);
            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取已发送好友申请时发生错误");
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<FriendshipDto>> UpdateFriendship(Guid id, UpdateFriendshipDto updateDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var friendship = await _friendshipService.UpdateAsync(id, updateDto, userId);
            return Ok(friendship);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新好友关系时发生错误，ID: {FriendshipId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost("blacklist")]
    public async Task<ActionResult<BlacklistDto>> AddToBlacklist(CreateBlacklistDto createDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var blacklist = await _friendshipService.AddToBlacklistAsync(createDto, userId);
            return CreatedAtAction(nameof(GetBlacklist), new { id = blacklist.Id }, blacklist);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加到黑名单时发生错误");
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpGet("blacklist")]
    public async Task<ActionResult<IEnumerable<BlacklistDto>>> GetBlacklist()
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var blacklist = await _friendshipService.GetBlacklistAsync(userId);
            return Ok(blacklist);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取黑名单时发生错误");
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpDelete("blacklist/{id}")]
    public async Task<ActionResult> RemoveFromBlacklist(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var result = await _friendshipService.RemoveFromBlacklistAsync(id, userId);
            if (!result)
                return NotFound("黑名单记录不存在");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从黑名单移除时发生错误，ID: {BlacklistId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpGet("is-friend/{friendId}")]
    public async Task<ActionResult<bool>> IsFriend(Guid friendId)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var isFriend = await _friendshipService.IsFriendAsync(userId, friendId);
            return Ok(isFriend);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查好友关系时发生错误，好友ID: {FriendId}", friendId);
            return StatusCode(500, "服务器内部错误");
        }
    }
}



