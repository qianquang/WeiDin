using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;

namespace WeiDin.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class FriendshipsController : ControllerBase
{
    private readonly IFriendshipService _friendshipService;
    private readonly ILogger<FriendshipsController> _logger;

    public FriendshipsController(IFriendshipService friendshipService, ILogger<FriendshipsController> logger)
    {
        _friendshipService = friendshipService;
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
    public async Task<ActionResult<FriendshipDto>> AddFriend(CreateFriendshipDto createDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var friendship = await _friendshipService.AddFriendAsync(createDto, userId);
            return CreatedAtAction(nameof(GetFriendship), new { id = friendship.Id }, friendship);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加好友时发生错误");
            return StatusCode(500, "服务器内部错误");
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
