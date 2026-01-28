using Volo.Abp.Application.Services;
using WeiDin.Application.DTOs;

namespace WeiDin.Application.Interfaces;

public interface IFriendshipService : IApplicationService
{
    Task<FriendshipDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<FriendshipDto>> GetByUserIdAsync(Guid userId);
    Task<FriendshipDto> AddFriendAsync(CreateFriendshipDto createDto, Guid userId);
    Task<bool> RemoveFriendAsync(Guid friendshipId, Guid userId);
    Task<FriendshipDto> UpdateAsync(Guid id, UpdateFriendshipDto updateDto, Guid userId);
    Task<bool> IsFriendAsync(Guid userId1, Guid userId2);
    Task<FriendshipDto> AcceptFriendRequestAsync(Guid friendshipId, Guid userId);
    Task<bool> RejectFriendRequestAsync(Guid friendshipId, Guid userId);
    Task<IEnumerable<FriendshipDto>> GetPendingRequestsAsync(Guid userId);
    Task<IEnumerable<FriendshipDto>> GetSentRequestsAsync(Guid userId);
    Task<BlacklistDto> AddToBlacklistAsync(CreateBlacklistDto createDto, Guid userId);
    Task<bool> RemoveFromBlacklistAsync(Guid blacklistId, Guid userId);
    Task<IEnumerable<BlacklistDto>> GetBlacklistAsync(Guid userId);
    Task<bool> IsBlacklistedAsync(Guid userId, Guid blockedUserId);
}



