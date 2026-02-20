using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Volo.Abp.AspNetCore.Mvc;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.API.Hubs;

namespace WeiDin.API.Controllers;

[Route("api/v1/[controller]")]
[Authorize]
public class GroupsController : AbpControllerBase
{
    private readonly IGroupService _groupService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<GroupsController> _logger;

    public GroupsController(IGroupService groupService, IHubContext<ChatHub> hubContext, ILogger<GroupsController> logger)
    {
        _groupService = groupService;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<GroupDto>>> GetAllGroups([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            if (page < 1)
                return BadRequest("页码必须大于0");
            
            if (pageSize < 1 || pageSize > 100)
                return BadRequest("每页数量必须在1-100之间");

            var groups = await _groupService.GetAllAsync(page, pageSize);
            _logger.LogInformation("成功获取群组列表，页码: {Page}, 每页数量: {PageSize}, 结果数量: {Count}", 
                page, pageSize, groups.Count());
            
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取群组列表时发生错误，页码: {Page}, 每页数量: {PageSize}", page, pageSize);
            return StatusCode(500, "获取群组列表失败");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GroupDto>> GetGroup(Guid id)
    {
        try
        {
            var group = await _groupService.GetByIdAsync(id);
            if (group == null)
            {
                _logger.LogWarning("群组不存在，群组ID: {GroupId}", id);
                return NotFound("群组不存在");
            }

            _logger.LogInformation("成功获取群组信息，群组ID: {GroupId}, 群组名称: {GroupName}", id, group.Name);
            return Ok(group);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取群组信息时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "获取群组信息失败");
        }
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<GroupDto>>> GetUserGroups(Guid userId)
    {
        try
        {
            var groups = await _groupService.GetByUserIdAsync(userId);
            _logger.LogInformation("成功获取用户群组列表，用户ID: {UserId}, 群组数量: {Count}", userId, groups.Count());
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户群组时发生错误，用户ID: {UserId}", userId);
            return StatusCode(500, "获取用户群组失败");
        }
    }

    [HttpGet("my-groups")]
    public async Task<ActionResult<IEnumerable<GroupDto>>> GetMyGroups()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("获取当前用户群组失败：无效的用户身份");
                return Unauthorized("无效的用户身份");
            }

            var groups = await _groupService.GetByUserIdAsync(userId);
            _logger.LogInformation("成功获取当前用户群组列表，用户ID: {UserId}, 群组数量: {Count}", userId, groups.Count());
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取当前用户群组时发生错误");
            return StatusCode(500, "获取当前用户群组失败");
        }
    }

    /// <summary>创建新群组</summary>
    [HttpPost]
    public async Task<ActionResult<GroupDto>> CreateGroup([FromBody] CreateGroupDto createGroupDto)
    {
        try
        {
            if (createGroupDto == null)
                return BadRequest("请求数据不能为空");

            if (string.IsNullOrWhiteSpace(createGroupDto.Name))
                return BadRequest("群组名称不能为空");

            if (createGroupDto.Name.Length > 100)
                return BadRequest("群组名称长度不能超过100个字符");

            if (createGroupDto.MaxMembers < 2 || createGroupDto.MaxMembers > 1000)
                return BadRequest("最大成员数必须在2-1000之间");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var ownerId))
            {
                _logger.LogWarning("创建群组失败：无效的用户身份");
                return Unauthorized("无效的用户身份");
            }

            var group = await _groupService.CreateAsync(createGroupDto, ownerId);
            
            _logger.LogInformation("群组创建成功: {GroupId}, 名称: {GroupName}, 创建者: {OwnerId}", 
                group.Id, group.Name, ownerId);
            
            // 发送 SignalR 通知给创建者
            try
            {
                await _hubContext.Clients.Group($"user_{ownerId}")
                    .SendAsync("GroupCreated", group);
                _logger.LogInformation("已通过 SignalR 发送群组创建通知给用户 {OwnerId}", ownerId);
            }
            catch (Exception ex)
            {
                // SignalR 通知失败不影响群组创建的成功响应
                _logger.LogWarning(ex, "发送群组创建通知失败，GroupId: {GroupId}", group.Id);
            }
            
            return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, group);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "创建群组时发生业务错误");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建群组时发生错误");
            return StatusCode(500, "创建群组失败");
        }
    }

    /// <summary>更新群组信息（仅群主和管理员可操作）</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<GroupDto>> UpdateGroup(Guid id, [FromBody] UpdateGroupDto updateGroupDto)
    {
        try
        {
            if (updateGroupDto == null)
                return BadRequest("请求数据不能为空");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("更新群组信息失败：无效的用户身份，群组ID: {GroupId}", id);
                return Unauthorized("无效的用户身份");
            }

            if (!string.IsNullOrEmpty(updateGroupDto.Name) && updateGroupDto.Name.Length > 100)
                return BadRequest("群组名称长度不能超过100个字符");

            if (updateGroupDto.MaxMembers.HasValue && (updateGroupDto.MaxMembers < 2 || updateGroupDto.MaxMembers > 1000))
                return BadRequest("最大成员数必须在2-1000之间");

            var group = await _groupService.UpdateAsync(id, updateGroupDto, userId);
            
            _logger.LogInformation("群组信息更新成功，群组ID: {GroupId}, 操作者: {UserId}", id, userId);
            
            // 发送 SignalR 通知给群组成员
            try
            {
                var members = await _groupService.GetMembersAsync(id);
                foreach (var member in members)
                {
                    await _hubContext.Clients.Group($"user_{member.UserId}")
                        .SendAsync("GroupUpdated", group);
                }
                _logger.LogInformation("已通过 SignalR 发送群组更新通知给群组 {GroupId} 的所有成员", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发送群组更新通知失败，GroupId: {GroupId}", id);
            }
            
            return Ok(group);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "更新群组信息失败：无权限，群组ID: {GroupId}", id);
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "更新群组信息失败：业务错误，群组ID: {GroupId}", id);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新群组信息时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "更新群组信息失败");
        }
    }

    /// <summary>删除群组（仅群主可操作，软删除）</summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteGroup(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("删除群组失败：无效的用户身份，群组ID: {GroupId}", id);
                return Unauthorized("无效的用户身份");
            }

            var result = await _groupService.DeleteAsync(id, userId);
            if (!result)
            {
                _logger.LogWarning("删除群组失败：群组不存在或无权限删除，群组ID: {GroupId}, 操作者: {UserId}", id, userId);
                return NotFound("群组不存在或无权限删除");
            }

            _logger.LogInformation("群组删除成功，群组ID: {GroupId}, 操作者: {UserId}", id, userId);
            
            // 发送 SignalR 通知给群组成员
            try
            {
                var members = await _groupService.GetMembersAsync(id);
                foreach (var member in members)
                {
                    await _hubContext.Clients.Group($"user_{member.UserId}")
                        .SendAsync("GroupDeleted", id);
                }
                _logger.LogInformation("已通过 SignalR 发送群组删除通知给群组 {GroupId} 的所有成员", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发送群组删除通知失败，GroupId: {GroupId}", id);
            }
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除群组时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "删除群组失败");
        }
    }

    /// <summary>加入群组</summary>
    [HttpPost("{id}/join")]
    public async Task<ActionResult> JoinGroup(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("加入群组失败：无效的用户身份，群组ID: {GroupId}", id);
                return Unauthorized("无效的用户身份");
            }

            var result = await _groupService.JoinGroupAsync(id, userId);
            if (!result)
            {
                _logger.LogWarning("加入群组失败：群组不存在、已满或用户已是成员，群组ID: {GroupId}, 用户ID: {UserId}", id, userId);
                return BadRequest("加入群组失败，可能是群组不存在、已满或您已经是成员");
            }

            _logger.LogInformation("用户 {UserId} 成功加入群组 {GroupId}", userId, id);
            
            // 发送 SignalR 通知给群组成员
            try
            {
                var group = await _groupService.GetByIdAsync(id);
                if (group != null)
                {
                    var members = await _groupService.GetMembersAsync(id);
                    foreach (var member in members)
                    {
                        await _hubContext.Clients.Group($"user_{member.UserId}")
                            .SendAsync("MemberJoined", new { GroupId = id, UserId = userId });
                    }
                    _logger.LogInformation("已通过 SignalR 发送成员加入通知给群组 {GroupId} 的所有成员", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发送成员加入通知失败，GroupId: {GroupId}", id);
            }
            
            return Ok(new { message = "成功加入群组", groupId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加入群组时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "加入群组失败");
        }
    }

    /// <summary>退出群组（群主不能退出，只能解散群组）</summary>
    [HttpPost("{id}/leave")]
    public async Task<ActionResult> LeaveGroup(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("退出群组失败：无效的用户身份，群组ID: {GroupId}", id);
                return Unauthorized("无效的用户身份");
            }

            var result = await _groupService.LeaveGroupAsync(id, userId);
            if (!result)
            {
                _logger.LogWarning("退出群组失败：用户不是群组成员或是群主，群组ID: {GroupId}, 用户ID: {UserId}", id, userId);
                return BadRequest("退出群组失败，可能是您不是群组成员或您是群主（群主不能退出，只能解散群组）");
            }

            _logger.LogInformation("用户 {UserId} 成功退出群组 {GroupId}", userId, id);
            
            // 发送 SignalR 通知给群组成员
            try
            {
                var members = await _groupService.GetMembersAsync(id);
                foreach (var member in members)
                {
                    await _hubContext.Clients.Group($"user_{member.UserId}")
                        .SendAsync("MemberLeft", new { GroupId = id, UserId = userId });
                }
                
                // 通知离开的用户离开群组 SignalR 组
                await _hubContext.Clients.Group($"user_{userId}")
                    .SendAsync("LeaveGroupNotification", id.ToString());
                
                _logger.LogInformation("已通过 SignalR 发送成员退出通知给群组 {GroupId} 的所有成员", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发送成员退出通知失败，GroupId: {GroupId}", id);
            }
            
            return Ok(new { message = "成功退出群组", groupId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "退出群组时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "退出群组失败");
        }
    }

    /// <summary>添加群组成员（仅管理员可操作）</summary>
    [HttpPost("{id}/members")]
    public async Task<ActionResult> AddMember(Guid id, [FromBody] AddGroupMemberDto addMemberDto)
    {
        try
        {
            if (addMemberDto == null)
                return BadRequest("请求数据不能为空");

            if (addMemberDto.UserId == Guid.Empty)
                return BadRequest("用户ID不能为空");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var operatorId))
            {
                _logger.LogWarning("添加群组成员失败：无效的用户身份，群组ID: {GroupId}", id);
                return Unauthorized("无效的用户身份");
            }

            var result = await _groupService.AddMemberAsync(id, addMemberDto, operatorId);
            if (!result)
            {
                _logger.LogWarning("添加群组成员失败：无权限、群组已满或用户已是成员，群组ID: {GroupId}, 操作者: {OperatorId}, 目标用户: {UserId}", 
                    id, operatorId, addMemberDto.UserId);
                return BadRequest("添加成员失败，可能是无权限、群组已满或用户已是成员");
            }

            _logger.LogInformation("操作者 {OperatorId} 成功将用户 {UserId} 添加到群组 {GroupId}", 
                operatorId, addMemberDto.UserId, id);
            
            // 发送 SignalR 通知给群组成员
            try
            {
                var members = await _groupService.GetMembersAsync(id);
                foreach (var member in members)
                {
                    await _hubContext.Clients.Group($"user_{member.UserId}")
                        .SendAsync("MemberAdded", new { GroupId = id, UserId = addMemberDto.UserId });
                }
                
                // 通知新添加的成员加入群组 SignalR 组
                await _hubContext.Clients.Group($"user_{addMemberDto.UserId}")
                    .SendAsync("JoinGroupNotification", id.ToString());
                
                _logger.LogInformation("已通过 SignalR 发送成员添加通知给群组 {GroupId} 的所有成员", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发送成员添加通知失败，GroupId: {GroupId}", id);
            }
            
            return Ok(new { message = "成功添加成员", groupId = id, userId = addMemberDto.UserId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加群组成员时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "添加成员失败");
        }
    }

    /// <summary>移除群组成员（仅管理员可操作，不能移除群主）</summary>
    [HttpDelete("{id}/members/{memberId}")]
    public async Task<ActionResult> RemoveMember(Guid id, Guid memberId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var operatorId))
            {
                _logger.LogWarning("移除群组成员失败：无效的用户身份，群组ID: {GroupId}, 成员ID: {MemberId}", id, memberId);
                return Unauthorized("无效的用户身份");
            }

            var result = await _groupService.RemoveMemberAsync(id, memberId, operatorId);
            if (!result)
            {
                _logger.LogWarning("移除群组成员失败：无权限、成员不存在或不能移除群主，群组ID: {GroupId}, 操作者: {OperatorId}, 成员ID: {MemberId}", 
                    id, operatorId, memberId);
                return BadRequest("移除成员失败，可能是无权限、成员不存在或不能移除群主");
            }

            _logger.LogInformation("操作者 {OperatorId} 成功将成员 {MemberId} 从群组 {GroupId} 移除", 
                operatorId, memberId, id);
            
            // 发送 SignalR 通知给群组成员
            try
            {
                var members = await _groupService.GetMembersAsync(id);
                foreach (var member in members)
                {
                    await _hubContext.Clients.Group($"user_{member.UserId}")
                        .SendAsync("MemberRemoved", new { GroupId = id, UserId = memberId });
                }
                // 也通知被移除的成员
                await _hubContext.Clients.Group($"user_{memberId}")
                    .SendAsync("MemberRemoved", new { GroupId = id, UserId = memberId });
                
                // 通知被移除的成员离开群组 SignalR 组
                await _hubContext.Clients.Group($"user_{memberId}")
                    .SendAsync("LeaveGroupNotification", id.ToString());
                
                _logger.LogInformation("已通过 SignalR 发送成员移除通知给群组 {GroupId} 的所有成员", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发送成员移除通知失败，GroupId: {GroupId}", id);
            }
            
            return Ok(new { message = "成功移除成员", groupId = id, memberId = memberId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移除群组成员时发生错误，群组ID: {GroupId}, 成员ID: {MemberId}", id, memberId);
            return StatusCode(500, "移除成员失败");
        }
    }

    /// <summary>更新群组成员信息（仅管理员可操作，如修改昵称、角色等）</summary>
    [HttpPut("{id}/members/{memberId}")]
    public async Task<ActionResult> UpdateMember(Guid id, Guid memberId, [FromBody] UpdateGroupMemberDto updateDto)
    {
        try
        {
            if (updateDto == null)
                return BadRequest("请求数据不能为空");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var operatorId))
            {
                _logger.LogWarning("更新群组成员失败：无效的用户身份，群组ID: {GroupId}, 成员ID: {MemberId}", id, memberId);
                return Unauthorized("无效的用户身份");
            }

            if (!string.IsNullOrEmpty(updateDto.Role) && updateDto.Role != "Owner" && updateDto.Role != "Admin" && updateDto.Role != "Member")
                return BadRequest("角色只能是 Owner、Admin 或 Member");

            var result = await _groupService.UpdateMemberAsync(id, memberId, updateDto, operatorId);
            if (!result)
            {
                _logger.LogWarning("更新群组成员失败：无权限或成员不存在，群组ID: {GroupId}, 操作者: {OperatorId}, 成员ID: {MemberId}", 
                    id, operatorId, memberId);
                return BadRequest("更新成员信息失败，可能是无权限或成员不存在");
            }

            _logger.LogInformation("操作者 {OperatorId} 成功更新群组 {GroupId} 中成员 {MemberId} 的信息", 
                operatorId, id, memberId);
            
            // 发送 SignalR 通知给群组成员
            try
            {
                var members = await _groupService.GetMembersAsync(id);
                foreach (var member in members)
                {
                    await _hubContext.Clients.Group($"user_{member.UserId}")
                        .SendAsync("MemberUpdated", new { GroupId = id, UserId = memberId, UpdateDto = updateDto });
                }
                _logger.LogInformation("已通过 SignalR 发送成员更新通知给群组 {GroupId} 的所有成员", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发送成员更新通知失败，GroupId: {GroupId}", id);
            }
            
            return Ok(new { message = "成功更新成员信息", groupId = id, memberId = memberId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新群组成员时发生错误，群组ID: {GroupId}, 成员ID: {MemberId}", id, memberId);
            return StatusCode(500, "更新成员信息失败");
        }
    }

    /// <summary>获取群组成员列表</summary>
    [HttpGet("{id}/members")]
    public async Task<ActionResult<IEnumerable<GroupMemberDto>>> GetGroupMembers(Guid id)
    {
        try
        {
            var members = await _groupService.GetMembersAsync(id);
            _logger.LogInformation("成功获取群组成员列表，群组ID: {GroupId}, 成员数量: {Count}", id, members.Count());
            return Ok(members);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取群组成员时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "获取群组成员失败");
        }
    }

    // ========== 群组申请相关端点 ==========

    /// <summary>申请加入群组</summary>
    [HttpPost("{id}/request")]
    public async Task<ActionResult<GroupMemberDto>> RequestJoinGroup(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("申请加入群组失败：无效的用户身份，群组ID: {GroupId}", id);
                return Unauthorized("无效的用户身份");
            }

            var request = await _groupService.RequestJoinGroupAsync(id, userId);
            
            _logger.LogInformation("用户 {UserId} 成功申请加入群组 {GroupId}", userId, id);
            
            // 发送 SignalR 通知给群主
            try
            {
                var group = await _groupService.GetByIdAsync(id);
                if (group != null)
                {
                    await _hubContext.Clients.Group($"user_{group.OwnerId}")
                        .SendAsync("GroupRequestReceived", request);
                    _logger.LogInformation("已通过 SignalR 发送群组申请通知给群主 {OwnerId}", group.OwnerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发送群组申请通知失败，GroupId: {GroupId}", id);
            }
            
            return CreatedAtAction(nameof(GetGroupMember), new { id = request.Id }, request);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "申请加入群组失败：业务错误，群组ID: {GroupId}", id);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "申请加入群组时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "申请加入群组失败");
        }
    }

    /// <summary>获取群组成员详情</summary>
    [HttpGet("members/{id}")]
    public async Task<ActionResult<GroupMemberDto>> GetGroupMember(Guid id)
    {
        // 这个端点主要用于 CreatedAtAction，实际可以通过其他端点获取
        return NotFound("请使用其他端点获取成员信息");
    }

    /// <summary>接受群组申请（仅群主）</summary>
    [HttpPost("members/{memberId}/accept")]
    public async Task<ActionResult<GroupMemberDto>> AcceptGroupRequest(Guid memberId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var ownerId))
            {
                _logger.LogWarning("接受群组申请失败：无效的用户身份，成员ID: {MemberId}", memberId);
                return Unauthorized("无效的用户身份");
            }

            var member = await _groupService.AcceptGroupRequestAsync(memberId, ownerId);
            
            _logger.LogInformation("群主 {OwnerId} 成功接受群组申请 {MemberId}", ownerId, memberId);
            
            // 发送 SignalR 通知给申请者
            try
            {
                await _hubContext.Clients.Group($"user_{member.UserId}")
                    .SendAsync("GroupRequestAccepted", new
                    {
                        RequestId = member.Id,
                        GroupId = member.GroupId,
                        GroupName = member.GroupName
                    });
                
                // 通知申请者已加入群组
                var group = await _groupService.GetByIdAsync(member.GroupId);
                if (group != null)
                {
                    await _hubContext.Clients.Group($"user_{member.UserId}")
                        .SendAsync("MemberJoined", new { GroupId = member.GroupId, UserId = member.UserId });
                    
                    // 通知申请者加入群组 SignalR 组
                    await _hubContext.Clients.Group($"user_{member.UserId}")
                        .SendAsync("JoinGroupNotification", member.GroupId.ToString());
                }
                
                _logger.LogInformation("已通过 SignalR 发送申请接受通知给申请者 {UserId}", member.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发送申请接受通知失败，MemberId: {MemberId}", memberId);
            }
            
            return Ok(member);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "接受群组申请失败：业务错误，成员ID: {MemberId}", memberId);
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "接受群组申请失败：无权限，成员ID: {MemberId}", memberId);
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接受群组申请时发生错误，成员ID: {MemberId}", memberId);
            return StatusCode(500, "接受群组申请失败");
        }
    }

    /// <summary>拒绝群组申请（仅群主）</summary>
    [HttpPost("members/{memberId}/reject")]
    public async Task<ActionResult> RejectGroupRequest(Guid memberId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var ownerId))
            {
                _logger.LogWarning("拒绝群组申请失败：无效的用户身份，成员ID: {MemberId}", memberId);
                return Unauthorized("无效的用户身份");
            }

            var member = await _groupService.RejectGroupRequestAsync(memberId, ownerId);
            if (member == null)
            {
                _logger.LogWarning("拒绝群组申请失败：申请不存在或无权限，成员ID: {MemberId}, 操作者: {OwnerId}", memberId, ownerId);
                return NotFound("申请不存在或无权限拒绝");
            }

            _logger.LogInformation("群主 {OwnerId} 成功拒绝群组申请 {MemberId}", ownerId, memberId);
            
            // 发送 SignalR 通知给申请者
            try
            {
                await _hubContext.Clients.Group($"user_{member.UserId}")
                    .SendAsync("GroupRequestRejected", new
                    {
                        RequestId = memberId,
                        GroupId = member.GroupId,
                        GroupName = member.GroupName
                    });
                _logger.LogInformation("已通过 SignalR 发送申请拒绝通知给申请者 {UserId}", member.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "发送申请拒绝通知失败，MemberId: {MemberId}", memberId);
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "拒绝群组申请失败：无权限，成员ID: {MemberId}", memberId);
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "拒绝群组申请时发生错误，成员ID: {MemberId}", memberId);
            return StatusCode(500, "拒绝群组申请失败");
        }
    }

    /// <summary>获取群组的待处理申请（仅群主）</summary>
    [HttpGet("{id}/requests/pending")]
    public async Task<ActionResult<IEnumerable<GroupMemberDto>>> GetPendingRequests(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var ownerId))
            {
                _logger.LogWarning("获取待处理申请失败：无效的用户身份，群组ID: {GroupId}", id);
                return Unauthorized("无效的用户身份");
            }

            var requests = await _groupService.GetPendingRequestsAsync(id, ownerId);
            _logger.LogInformation("成功获取群组 {GroupId} 的待处理申请列表，数量: {Count}", id, requests.Count());
            return Ok(requests);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "获取待处理申请失败：无权限，群组ID: {GroupId}", id);
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取待处理申请时发生错误，群组ID: {GroupId}", id);
            return StatusCode(500, "获取待处理申请失败");
        }
    }

    /// <summary>获取当前用户已发送的群组申请</summary>
    [HttpGet("requests/sent")]
    public async Task<ActionResult<IEnumerable<GroupMemberDto>>> GetSentRequests()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("获取已发送申请失败：无效的用户身份");
                return Unauthorized("无效的用户身份");
            }

            var requests = await _groupService.GetSentRequestsAsync(userId);
            _logger.LogInformation("成功获取用户 {UserId} 的已发送申请列表，数量: {Count}", userId, requests.Count());
            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取已发送申请时发生错误");
            return StatusCode(500, "获取已发送申请失败");
        }
    }
}



