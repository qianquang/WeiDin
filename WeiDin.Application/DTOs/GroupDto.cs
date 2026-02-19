namespace WeiDin.Application.DTOs;

public class GroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Avatar { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? Announcement { get; set; }
    public int MaxMembers { get; set; }
    public int CurrentMembers { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<GroupMemberDto> Members { get; set; } = new();
}

public class CreateGroupDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Avatar { get; set; }
    public string? Announcement { get; set; }
    public int MaxMembers { get; set; } = 500;
}

public class UpdateGroupDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Avatar { get; set; }
    public string? Announcement { get; set; }
    public int? MaxMembers { get; set; }
}

public class GroupMemberDto
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string? GroupName { get; set; }  // 用于申请列表显示群组名称
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; }
}

public class AddGroupMemberDto
{
    public Guid UserId { get; set; }
    public string? Nickname { get; set; }
}

public class UpdateGroupMemberDto
{
    public string? Nickname { get; set; }
    public string? Role { get; set; }
}

