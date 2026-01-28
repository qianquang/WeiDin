using System.Text.Json.Serialization;

namespace WeiDin.Application.DTOs;

public class FriendshipDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public Guid FriendId { get; set; }
    public string FriendName { get; set; } = string.Empty;
    public string? FriendAvatar { get; set; }
    public string? GroupName { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class CreateFriendshipDto
{
    [JsonPropertyName("friendId")]
    public Guid FriendId { get; set; }
    
    [JsonPropertyName("groupName")]
    public string? GroupName { get; set; }
    
    [JsonPropertyName("remark")]
    public string? Remark { get; set; }
}

public class UpdateFriendshipDto
{
    public string? GroupName { get; set; }
    public string? Remark { get; set; }
}

public class BlacklistDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid BlockedUserId { get; set; }
    public string BlockedUserName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBlacklistDto
{
    public Guid BlockedUserId { get; set; }
    public string? Reason { get; set; }
}



