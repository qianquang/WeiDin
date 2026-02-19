using System.Reflection;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.Core.Entities;
using WeiDin.Core.Enums;
using WeiDin.Core.Interfaces;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace WeiDin.Application.Services;

public class GroupService : ApplicationService, IGroupService
{
    private readonly IRepository<Group, Guid> _groupRepository;
    private readonly IRepository<GroupMember, Guid> _groupMemberRepository;
    private readonly IRepository<Conversation, Guid> _conversationRepository;
    private readonly IDynamicTableService _dynamicTableService;
    private readonly IMapper _mapper;

    public GroupService(IRepository<Group, Guid> groupRepository,
                        IRepository<GroupMember, Guid> groupMemberRepository,
                        IRepository<Conversation, Guid> conversationRepository,
                        IDynamicTableService dynamicTableService,
                        IMapper mapper)
    {
        _groupRepository = groupRepository;
        _groupMemberRepository = groupMemberRepository;
        _conversationRepository = conversationRepository;
        _dynamicTableService = dynamicTableService;
        _mapper = mapper;
    }

    public async Task<GroupDto?> GetByIdAsync(Guid id)
    {
        var queryable = await _groupRepository.GetQueryableAsync();
        var group = await queryable
            .Include(g => g.Owner)
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null)
            return null;

        return _mapper.Map<GroupDto>(group);
    }

    public async Task<IEnumerable<GroupDto>> GetByUserIdAsync(Guid userId)
    {
        var groupMembers = await _groupMemberRepository.GetListAsync(gm => 
            gm.UserId == userId && gm.IsActive);
        
        var groupIds = groupMembers.Select(gm => gm.GroupId).ToList();
        
        if (!groupIds.Any())
            return Enumerable.Empty<GroupDto>();

        var queryable = await _groupRepository.GetQueryableAsync();
        var groups = await queryable
            .Include(g => g.Owner)
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .Where(g => groupIds.Contains(g.Id) && g.IsActive)
            .ToListAsync();

        return _mapper.Map<IEnumerable<GroupDto>>(groups);
    }

    public async Task<IEnumerable<GroupDto>> GetAllAsync(int page = 1, int pageSize = 20)
    {
        var queryable = await _groupRepository.GetQueryableAsync();
        var groups = await queryable
            .Include(g => g.Owner)
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .Where(g => g.IsActive)
            .OrderByDescending(g => g.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return _mapper.Map<IEnumerable<GroupDto>>(groups);
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

        await _groupRepository.InsertAsync(group, autoSave: true);

        // 设置 ConversationId = GroupId，创建 Conversation 和动态分表
        group.ConversationId = group.Id;
        await _groupRepository.UpdateAsync(group, autoSave: true);

        var conversation = new Conversation
        {
            RelationId = group.Id,
            RelationType = RelationType.Group,
            CreatedAt = DateTime.UtcNow
        };
        // 使用反射设置 Id（因为 Entity<Guid>.Id 是 protected set）
        typeof(Conversation).GetProperty("Id")!.SetValue(conversation, group.Id);
        await _conversationRepository.InsertAsync(conversation, autoSave: true);
        await _dynamicTableService.EnsureConversationTablesAsync(group.Id);

        // 添加群主为成员
        var ownerMember = new GroupMember
        {
            GroupId = group.Id,
            UserId = ownerId,
            Role = "Owner"
        };
        await _groupMemberRepository.InsertAsync(ownerMember, autoSave: true);

        // 重新加载群组及其相关数据
        var queryable = await _groupRepository.GetQueryableAsync();
        var loadedGroup = await queryable
            .Include(g => g.Owner)
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == group.Id);

        return _mapper.Map<GroupDto>(loadedGroup!);
    }

    public async Task<GroupDto> UpdateAsync(Guid id, UpdateGroupDto updateDto, Guid userId)
    {
        var group = await _groupRepository.FindAsync(id);
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

        await _groupRepository.UpdateAsync(group, autoSave: true);

        // 重新加载群组及其相关数据
        var queryable = await _groupRepository.GetQueryableAsync();
        var loadedGroup = await queryable
            .Include(g => g.Owner)
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == id);

        return _mapper.Map<GroupDto>(loadedGroup!);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        var group = await _groupRepository.FindAsync(id);
        if (group == null)
            return false;

        if (!await IsOwnerAsync(id, userId))
            return false;

        var conversationId = group.ConversationId;

        // 软删除群组
        group.IsActive = false;
        group.UpdatedAt = DateTime.UtcNow;
        await _groupRepository.UpdateAsync(group, autoSave: true);

        // 删除对应的 Conversation 和分表
        if (conversationId.HasValue)
        {
            var conversation = await _conversationRepository.FindAsync(conversationId.Value);
            if (conversation != null)
            {
                await _conversationRepository.DeleteAsync(conversation);
                await _dynamicTableService.DeleteConversationTablesAsync(conversationId.Value);
            }
        }

        return true;
    }

    public async Task<bool> JoinGroupAsync(Guid groupId, Guid userId)
    {
        var group = await _groupRepository.FindAsync(groupId);
        if (group == null || !group.IsActive)
            return false;

        // 检查是否已经是成员
        if (await IsMemberAsync(groupId, userId))
            return false;

        // 检查群组是否已满
        var currentMemberCount = await _groupMemberRepository.CountAsync(gm => 
            gm.GroupId == groupId && gm.IsActive);
        if (currentMemberCount >= group.MaxMembers)
            return false;

        var member = new GroupMember
        {
            GroupId = groupId,
            UserId = userId,
            Role = "Member"
        };

        await _groupMemberRepository.InsertAsync(member, autoSave: true);
        return true;
    }

    public async Task<bool> LeaveGroupAsync(Guid groupId, Guid userId)
    {
        var member = await _groupMemberRepository.FirstOrDefaultAsync(gm => 
            gm.GroupId == groupId && gm.UserId == userId && gm.IsActive);

        if (member == null)
            return false;

        // 群主不能退出群组，只能解散群组
        if (member.Role == "Owner")
            return false;

        member.IsActive = false;
        member.LeftAt = DateTime.UtcNow;

        await _groupMemberRepository.UpdateAsync(member, autoSave: true);
        return true;
    }

    public async Task<bool> AddMemberAsync(Guid groupId, AddGroupMemberDto addMemberDto, Guid operatorId)
    {
        if (!await IsAdminAsync(groupId, operatorId))
            return false;

        var group = await _groupRepository.FindAsync(groupId);
        if (group == null || !group.IsActive)
            return false;

        // 检查群组是否已满
        var currentMemberCount = await _groupMemberRepository.CountAsync(gm => 
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

        await _groupMemberRepository.InsertAsync(member, autoSave: true);
        return true;
    }

    public async Task<bool> RemoveMemberAsync(Guid groupId, Guid memberId, Guid operatorId)
    {
        if (!await IsAdminAsync(groupId, operatorId))
            return false;

        var member = await _groupMemberRepository.FirstOrDefaultAsync(gm => 
            gm.GroupId == groupId && gm.UserId == memberId && gm.IsActive);

        if (member == null)
            return false;

        // 不能移除群主
        if (member.Role == "Owner")
            return false;

        member.IsActive = false;
        member.LeftAt = DateTime.UtcNow;

        await _groupMemberRepository.UpdateAsync(member, autoSave: true);
        return true;
    }

    public async Task<bool> UpdateMemberAsync(Guid groupId, Guid memberId, UpdateGroupMemberDto updateDto, Guid operatorId)
    {
        if (!await IsAdminAsync(groupId, operatorId))
            return false;

        var member = await _groupMemberRepository.FirstOrDefaultAsync(gm => 
            gm.GroupId == groupId && gm.UserId == memberId && gm.IsActive);

        if (member == null)
            return false;

        if (!string.IsNullOrEmpty(updateDto.Nickname))
            member.Nickname = updateDto.Nickname;

        if (!string.IsNullOrEmpty(updateDto.Role) && updateDto.Role != "Owner")
            member.Role = updateDto.Role;

        await _groupMemberRepository.UpdateAsync(member, autoSave: true);
        return true;
    }

    public async Task<IEnumerable<GroupMemberDto>> GetMembersAsync(Guid groupId)
    {
        var queryable = await _groupMemberRepository.GetQueryableAsync();
        var members = await queryable
            .Include(m => m.User)
            .Where(gm => gm.GroupId == groupId && gm.IsActive)
            .ToListAsync();

        return _mapper.Map<IEnumerable<GroupMemberDto>>(members);
    }

    public async Task<bool> IsMemberAsync(Guid groupId, Guid userId)
    {
        return await _groupMemberRepository.AnyAsync(gm => 
            gm.GroupId == groupId && gm.UserId == userId && gm.IsActive);
    }

    public async Task<bool> IsOwnerAsync(Guid groupId, Guid userId)
    {
        return await _groupMemberRepository.AnyAsync(gm => 
            gm.GroupId == groupId && gm.UserId == userId && gm.Role == "Owner" && gm.IsActive);
    }

    public async Task<bool> IsAdminAsync(Guid groupId, Guid userId)
    {
        return await _groupMemberRepository.AnyAsync(gm => 
            gm.GroupId == groupId && gm.UserId == userId && 
            (gm.Role == "Owner" || gm.Role == "Admin") && gm.IsActive);
    }

    // ========== 群组申请相关方法（使用 GroupMember 的 IsActive 字段） ==========

    public async Task<GroupMemberDto> RequestJoinGroupAsync(Guid groupId, Guid userId)
    {
        var group = await _groupRepository.FindAsync(groupId);
        if (group == null || !group.IsActive)
            throw new InvalidOperationException("群组不存在或已解散");

        // 检查是否已经是成员（IsActive=true）
        if (await IsMemberAsync(groupId, userId))
            throw new InvalidOperationException("您已经是该群组的成员");

        // 检查是否已有待处理的申请（IsActive=false）
        var existingRequest = await _groupMemberRepository.FirstOrDefaultAsync(gm =>
            gm.GroupId == groupId && gm.UserId == userId && !gm.IsActive);
        if (existingRequest != null)
            throw new InvalidOperationException("您已提交过申请，请等待处理");

        // 检查群组是否已满
        var currentMemberCount = await _groupMemberRepository.CountAsync(gm =>
            gm.GroupId == groupId && gm.IsActive);
        if (currentMemberCount >= group.MaxMembers)
            throw new InvalidOperationException("群组已满，无法申请加入");

        // 创建群组申请（IsActive=false）
        var member = new GroupMember
        {
            GroupId = groupId,
            UserId = userId,
            Role = "Member", // 申请时先设置为 Member，接受后保持
            IsActive = false, // 申请状态，等待群主同意
            JoinedAt = DateTime.UtcNow
        };

        await _groupMemberRepository.InsertAsync(member, autoSave: true);

        // 重新加载以包含导航属性
        var queryable = await _groupMemberRepository.GetQueryableAsync();
        var loadedMember = await queryable
            .Include(gm => gm.Group)
            .Include(gm => gm.User)
            .FirstOrDefaultAsync(gm => gm.Id == member.Id);

        return _mapper.Map<GroupMemberDto>(loadedMember ?? member);
    }

    public async Task<GroupMemberDto> AcceptGroupRequestAsync(Guid memberId, Guid ownerId)
    {
        var queryable = await _groupMemberRepository.GetQueryableAsync();
        var member = await queryable
            .Include(gm => gm.Group)
            .Include(gm => gm.User)
            .FirstOrDefaultAsync(gm => gm.Id == memberId && !gm.IsActive);

        if (member == null)
            throw new InvalidOperationException("申请不存在或已被处理");

        // 验证是否为群主
        if (member.Group.OwnerId != ownerId)
            throw new UnauthorizedAccessException("只有群主可以接受申请");

        // 检查群组是否已满
        var currentMemberCount = await _groupMemberRepository.CountAsync(gm =>
            gm.GroupId == member.GroupId && gm.IsActive);
        if (currentMemberCount >= member.Group.MaxMembers)
            throw new InvalidOperationException("群组已满，无法接受申请");

        // 检查是否已经是成员
        var existingMember = await _groupMemberRepository.FirstOrDefaultAsync(gm =>
            gm.GroupId == member.GroupId && gm.UserId == member.UserId && gm.IsActive);
        if (existingMember != null)
        {
            // 如果已经是成员，删除申请记录
            await _groupMemberRepository.DeleteAsync(member);
            throw new InvalidOperationException("用户已经是群组成员");
        }

        // 激活成员（接受申请）
        member.IsActive = true;
        member.Role = "Member";
        member.JoinedAt = DateTime.UtcNow;
        await _groupMemberRepository.UpdateAsync(member, autoSave: true);

        return _mapper.Map<GroupMemberDto>(member);
    }

    public async Task<GroupMemberDto?> RejectGroupRequestAsync(Guid memberId, Guid ownerId)
    {
        var queryable = await _groupMemberRepository.GetQueryableAsync();
        var member = await queryable
            .Include(gm => gm.Group)
            .Include(gm => gm.User)
            .FirstOrDefaultAsync(gm => gm.Id == memberId && !gm.IsActive);

        if (member == null)
            return null;

        // 验证是否为群主
        if (member.Group.OwnerId != ownerId)
            return null;

        // 删除申请记录（拒绝申请）
        await _groupMemberRepository.DeleteAsync(member);

        return _mapper.Map<GroupMemberDto>(member);
    }

    public async Task<IEnumerable<GroupMemberDto>> GetPendingRequestsAsync(Guid groupId, Guid ownerId)
    {
        // 验证是否为群主
        if (!await IsOwnerAsync(groupId, ownerId))
            throw new UnauthorizedAccessException("只有群主可以查看待处理申请");

        var queryable = await _groupMemberRepository.GetQueryableAsync();
        var requests = await queryable
            .Include(gm => gm.Group)
            .Include(gm => gm.User)
            .Where(gm => gm.GroupId == groupId && !gm.IsActive)
            .OrderByDescending(gm => gm.JoinedAt)
            .ToListAsync();

        return _mapper.Map<IEnumerable<GroupMemberDto>>(requests);
    }

    public async Task<IEnumerable<GroupMemberDto>> GetSentRequestsAsync(Guid userId)
    {
        var queryable = await _groupMemberRepository.GetQueryableAsync();
        var requests = await queryable
            .Include(gm => gm.Group)
            .Include(gm => gm.User)
            .Where(gm => gm.UserId == userId && !gm.IsActive)
            .OrderByDescending(gm => gm.JoinedAt)
            .ToListAsync();

        return _mapper.Map<IEnumerable<GroupMemberDto>>(requests);
    }
}

