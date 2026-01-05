using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;

namespace WeiDin.API.Controllers;

[Route("api/v1/[controller]")]
[Authorize]
public class GroupsController : AbpControllerBase
{
    private readonly IGroupService _groupService;
    private readonly ILogger<GroupsController> _logger;

    public GroupsController(IGroupService groupService, ILogger<GroupsController> logger)
    {
        _groupService = groupService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<GroupDto>>> GetAllGroups([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var groups = await _groupService.GetAllAsync(page, pageSize);
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取群组列表时发生错误");
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GroupDto>> GetGroup(Guid id)
    {
        try
        {
            var group = await _groupService.GetByIdAsync(id);
            if (group == null)
                return NotFound("群组不存在");

            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取群组信息时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<GroupDto>>> GetUserGroups(Guid userId)
    {
        try
        {
            var groups = await _groupService.GetByUserIdAsync(userId);
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户群组时发生错误，用户ID: {UserId}", userId);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost]
    public async Task<ActionResult<GroupDto>> CreateGroup(CreateGroupDto createGroupDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var ownerId))
                return Unauthorized("无效的用户身份");

            var group = await _groupService.CreateAsync(createGroupDto, ownerId);
            return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建群组时发生错误");
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<GroupDto>> UpdateGroup(Guid id, UpdateGroupDto updateGroupDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var group = await _groupService.UpdateAsync(id, updateGroupDto, userId);
            return Ok(group);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新群组信息时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteGroup(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var result = await _groupService.DeleteAsync(id, userId);
            if (!result)
                return NotFound("群组不存在或无权限删除");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除群组时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost("{id}/join")]
    public async Task<ActionResult> JoinGroup(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var result = await _groupService.JoinGroupAsync(id, userId);
            if (!result)
                return BadRequest("加入群组失败");

            return Ok("成功加入群组");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加入群组时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost("{id}/leave")]
    public async Task<ActionResult> LeaveGroup(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized("无效的用户身份");

            var result = await _groupService.LeaveGroupAsync(id, userId);
            if (!result)
                return BadRequest("退出群组失败");

            return Ok("成功退出群组");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "退出群组时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPost("{id}/members")]
    public async Task<ActionResult> AddMember(Guid id, AddGroupMemberDto addMemberDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var operatorId))
                return Unauthorized("无效的用户身份");

            var result = await _groupService.AddMemberAsync(id, addMemberDto, operatorId);
            if (!result)
                return BadRequest("添加成员失败");

            return Ok("成功添加成员");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加群组成员时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpDelete("{id}/members/{memberId}")]
    public async Task<ActionResult> RemoveMember(Guid id, Guid memberId)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var operatorId))
                return Unauthorized("无效的用户身份");

            var result = await _groupService.RemoveMemberAsync(id, memberId, operatorId);
            if (!result)
                return BadRequest("移除成员失败");

            return Ok("成功移除成员");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移除群组成员时发生错误，群组ID: {GroupId}, 成员ID: {MemberId}", id, memberId);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpPut("{id}/members/{memberId}")]
    public async Task<ActionResult> UpdateMember(Guid id, Guid memberId, UpdateGroupMemberDto updateDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var operatorId))
                return Unauthorized("无效的用户身份");

            var result = await _groupService.UpdateMemberAsync(id, memberId, updateDto, operatorId);
            if (!result)
                return BadRequest("更新成员信息失败");

            return Ok("成功更新成员信息");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新群组成员时发生错误，群组ID: {GroupId}, 成员ID: {MemberId}", id, memberId);
            return StatusCode(500, "服务器内部错误");
        }
    }

    [HttpGet("{id}/members")]
    public async Task<ActionResult<IEnumerable<GroupMemberDto>>> GetGroupMembers(Guid id)
    {
        try
        {
            var members = await _groupService.GetMembersAsync(id);
            return Ok(members);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取群组成员时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "服务器内部错误");
        }
    }
}



