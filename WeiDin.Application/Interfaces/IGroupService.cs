using WeiDin.Application.DTOs;

namespace WeiDin.Application.Interfaces;

public interface IGroupService
{
    Task<GroupDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<GroupDto>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<GroupDto>> GetAllAsync(int page = 1, int pageSize = 20);
    Task<GroupDto> CreateAsync(CreateGroupDto createGroupDto, Guid ownerId);
    Task<GroupDto> UpdateAsync(Guid id, UpdateGroupDto updateGroupDto, Guid userId);
    Task<bool> DeleteAsync(Guid id, Guid userId);
    Task<bool> JoinGroupAsync(Guid groupId, Guid userId);
    Task<bool> LeaveGroupAsync(Guid groupId, Guid userId);
    Task<bool> AddMemberAsync(Guid groupId, AddGroupMemberDto addMemberDto, Guid operatorId);
    Task<bool> RemoveMemberAsync(Guid groupId, Guid memberId, Guid operatorId);
    Task<bool> UpdateMemberAsync(Guid groupId, Guid memberId, UpdateGroupMemberDto updateDto, Guid operatorId);
    Task<IEnumerable<GroupMemberDto>> GetMembersAsync(Guid groupId);
    Task<bool> IsMemberAsync(Guid groupId, Guid userId);
    Task<bool> IsOwnerAsync(Guid groupId, Guid userId);
    Task<bool> IsAdminAsync(Guid groupId, Guid userId);
}
