using AutoMapper;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.Core.Entities;
using WeiDin.Core.Interfaces;

namespace WeiDin.Application.Services;

public class GroupService : IGroupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public GroupService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<GroupDto?> GetByIdAsync(Guid id)
    {
        var group = await _unitOfWork.Groups.GetByIdAsync(id);
        if (group == null)
            return null;

        // 加载相关数据
        await LoadGroupRelatedData(group);
        return _mapper.Map<GroupDto>(group);
    }

    public async Task<IEnumerable<GroupDto>> GetByUserIdAsync(Guid userId)
    {
        var groupMembers = await _unitOfWork.GroupMembers.FindAsync(gm => 
            gm.UserId == userId && gm.IsActive);
        
        var groupIds = groupMembers.Select(gm => gm.GroupId).ToList();
        var groups = await _unitOfWork.Groups.FindAsync(g => groupIds.Contains(g.Id));

        foreach (var group in groups)
        {
            await LoadGroupRelatedData(group);
        }

        return _mapper.Map<IEnumerable<GroupDto>>(groups);
    }

    public async Task<IEnumerable<GroupDto>> GetAllAsync(int page = 1, int pageSize = 20)
    {
        var groups = await _unitOfWork.Groups.FindAsync(g => g.IsActive);
        
        var pagedGroups = groups
            .OrderByDescending(g => g.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        foreach (var group in pagedGroups)
        {
            await LoadGroupRelatedData(group);
        }

        return _mapper.Map<IEnumerable<GroupDto>>(pagedGroups);
    }

    public async Task<GroupDto> CreateAsync(CreateGroupDto createGroupDto, Guid ownerId)
    {
        var group = new Group
        {
            Name = createGroupDto.Name,
            Description = createGroupDto.Description,
            Avatar = createGroupDto.Avatar,
            OwnerId = ownerId,
            Announcement = createGroupDto.Announcement,
            MaxMembers = createGroupDto.MaxMembers
        };

        await _unitOfWork.Groups.AddAsync(group);
        await _unitOfWork.SaveChangesAsync();

        // 添加群主为成员
        var ownerMember = new GroupMember
        {
            GroupId = group.Id,
            UserId = ownerId,
            Role = "Owner"
        };
        await _unitOfWork.GroupMembers.AddAsync(ownerMember);
        await _unitOfWork.SaveChangesAsync();

        await LoadGroupRelatedData(group);
        return _mapper.Map<GroupDto>(group);
    }

    public async Task<GroupDto> UpdateAsync(Guid id, UpdateGroupDto updateDto, Guid userId)
    {
        var group = await _unitOfWork.Groups.GetByIdAsync(id);
        if (group == null)
            throw new InvalidOperationException("群组不存在");

        if (!await IsOwnerAsync(id, userId) && !await IsAdminAsync(id, userId))
            throw new UnauthorizedAccessException("无权限修改群组信息");

        if (!string.IsNullOrEmpty(updateDto.Name))
            group.Name = updateDto.Name;
        if (updateDto.Description != null)
            group.Description = updateDto.Description;
        if (updateDto.Avatar != null)
            group.Avatar = updateDto.Avatar;
        if (updateDto.Announcement != null)
            group.Announcement = updateDto.Announcement;
        if (updateDto.MaxMembers.HasValue)
            group.MaxMembers = updateDto.MaxMembers.Value;

        group.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Groups.UpdateAsync(group);
        await _unitOfWork.SaveChangesAsync();

        await LoadGroupRelatedData(group);
        return _mapper.Map<GroupDto>(group);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        var group = await _unitOfWork.Groups.GetByIdAsync(id);
        if (group == null)
            return false;

        if (!await IsOwnerAsync(id, userId))
            return false;

        group.IsActive = false;
        group.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Groups.UpdateAsync(group);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> JoinGroupAsync(Guid groupId, Guid userId)
    {
        var group = await _unitOfWork.Groups.GetByIdAsync(groupId);
        if (group == null || !group.IsActive)
            return false;

        // 检查是否已经是成员
        if (await IsMemberAsync(groupId, userId))
            return false;

        // 检查群组是否已满
        var currentMemberCount = await _unitOfWork.GroupMembers.CountAsync(gm => 
            gm.GroupId == groupId && gm.IsActive);
        if (currentMemberCount >= group.MaxMembers)
            return false;

        var member = new GroupMember
        {
            GroupId = groupId,
            UserId = userId,
            Role = "Member"
        };

        await _unitOfWork.GroupMembers.AddAsync(member);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LeaveGroupAsync(Guid groupId, Guid userId)
    {
        var member = await _unitOfWork.GroupMembers.FirstOrDefaultAsync(gm => 
            gm.GroupId == groupId && gm.UserId == userId && gm.IsActive);

        if (member == null)
            return false;

        // 群主不能退出群组，只能解散群组
        if (member.Role == "Owner")
            return false;

        member.IsActive = false;
        member.LeftAt = DateTime.UtcNow;

        await _unitOfWork.GroupMembers.UpdateAsync(member);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddMemberAsync(Guid groupId, AddGroupMemberDto addMemberDto, Guid operatorId)
    {
        if (!await IsAdminAsync(groupId, operatorId))
            return false;

        var group = await _unitOfWork.Groups.GetByIdAsync(groupId);
        if (group == null || !group.IsActive)
            return false;

        // 检查群组是否已满
        var currentMemberCount = await _unitOfWork.GroupMembers.CountAsync(gm => 
            gm.GroupId == groupId && gm.IsActive);
        if (currentMemberCount >= group.MaxMembers)
            return false;

        // 检查用户是否已经是成员
        if (await IsMemberAsync(groupId, addMemberDto.UserId))
            return false;

        var member = new GroupMember
        {
            GroupId = groupId,
            UserId = addMemberDto.UserId,
            Role = "Member",
            Nickname = addMemberDto.Nickname
        };

        await _unitOfWork.GroupMembers.AddAsync(member);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveMemberAsync(Guid groupId, Guid memberId, Guid operatorId)
    {
        if (!await IsAdminAsync(groupId, operatorId))
            return false;

        var member = await _unitOfWork.GroupMembers.FirstOrDefaultAsync(gm => 
            gm.GroupId == groupId && gm.UserId == memberId && gm.IsActive);

        if (member == null)
            return false;

        // 不能移除群主
        if (member.Role == "Owner")
            return false;

        member.IsActive = false;
        member.LeftAt = DateTime.UtcNow;

        await _unitOfWork.GroupMembers.UpdateAsync(member);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateMemberAsync(Guid groupId, Guid memberId, UpdateGroupMemberDto updateDto, Guid operatorId)
    {
        if (!await IsAdminAsync(groupId, operatorId))
            return false;

        var member = await _unitOfWork.GroupMembers.FirstOrDefaultAsync(gm => 
            gm.GroupId == groupId && gm.UserId == memberId && gm.IsActive);

        if (member == null)
            return false;

        if (!string.IsNullOrEmpty(updateDto.Nickname))
            member.Nickname = updateDto.Nickname;

        if (!string.IsNullOrEmpty(updateDto.Role) && updateDto.Role != "Owner")
            member.Role = updateDto.Role;

        await _unitOfWork.GroupMembers.UpdateAsync(member);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<GroupMemberDto>> GetMembersAsync(Guid groupId)
    {
        var members = await _unitOfWork.GroupMembers.FindAsync(gm => 
            gm.GroupId == groupId && gm.IsActive);

        return _mapper.Map<IEnumerable<GroupMemberDto>>(members);
    }

    public async Task<bool> IsMemberAsync(Guid groupId, Guid userId)
    {
        return await _unitOfWork.GroupMembers.ExistsAsync(gm => 
            gm.GroupId == groupId && gm.UserId == userId && gm.IsActive);
    }

    public async Task<bool> IsOwnerAsync(Guid groupId, Guid userId)
    {
        return await _unitOfWork.GroupMembers.ExistsAsync(gm => 
            gm.GroupId == groupId && gm.UserId == userId && gm.Role == "Owner" && gm.IsActive);
    }

    public async Task<bool> IsAdminAsync(Guid groupId, Guid userId)
    {
        return await _unitOfWork.GroupMembers.ExistsAsync(gm => 
            gm.GroupId == groupId && gm.UserId == userId && 
            (gm.Role == "Owner" || gm.Role == "Admin") && gm.IsActive);
    }

    private async Task LoadGroupRelatedData(Group group)
    {
        // 这里可以添加预加载相关数据的逻辑
        // 由于我们使用的是简单的Repository模式，这里暂时不实现
    }
}
