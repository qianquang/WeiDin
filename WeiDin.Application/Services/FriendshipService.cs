using AutoMapper;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.Core.Entities;
using Volo.Abp.Domain.Repositories;

namespace WeiDin.Application.Services;

public class FriendshipService : IFriendshipService
{
    private readonly IRepository<Friendship, Guid> _friendshipRepository;
    private readonly IRepository<Blacklist, Guid> _blacklistRepository;
    private readonly IMapper _mapper;

    public FriendshipService(IRepository<Friendship, Guid> friendshipRepository,
                             IRepository<Blacklist, Guid> blacklistRepository,
                             IMapper mapper)
    {
        _friendshipRepository = friendshipRepository;
        _blacklistRepository = blacklistRepository;
        _mapper = mapper;
    }

    public async Task<FriendshipDto?> GetByIdAsync(Guid id)
    {
        var friendship = await _friendshipRepository.FindAsync(id);
        if (friendship == null)
            return null;

        return _mapper.Map<FriendshipDto>(friendship);
    }

    public async Task<IEnumerable<FriendshipDto>> GetByUserIdAsync(Guid userId)
    {
        var friendships = await _friendshipRepository.GetListAsync(f => 
            f.UserId == userId && f.IsActive);

        return _mapper.Map<IEnumerable<FriendshipDto>>(friendships);
    }

    public async Task<FriendshipDto> AddFriendAsync(CreateFriendshipDto createDto, Guid userId)
    {
        // 检查是否已经是好友
        if (await IsFriendAsync(userId, createDto.FriendId))
            throw new InvalidOperationException("已经是好友关系");

        // 检查是否在黑名单中
        if (await IsBlacklistedAsync(userId, createDto.FriendId))
            throw new InvalidOperationException("该用户已被拉黑");

        var friendship = new Friendship
        {
            UserId = userId,
            FriendId = createDto.FriendId,
            GroupName = createDto.GroupName,
            Remark = createDto.Remark
        };

        await _friendshipRepository.InsertAsync(friendship, autoSave: true);

        return _mapper.Map<FriendshipDto>(friendship);
    }

    public async Task<bool> RemoveFriendAsync(Guid friendshipId, Guid userId)
    {
        var friendship = await _friendshipRepository.FindAsync(friendshipId);
        if (friendship == null || friendship.UserId != userId)
            return false;

        friendship.IsActive = false;
        friendship.UpdatedAt = DateTime.UtcNow;

        await _friendshipRepository.UpdateAsync(friendship, autoSave: true);
        return true;
    }

    public async Task<FriendshipDto> UpdateAsync(Guid id, UpdateFriendshipDto updateDto, Guid userId)
    {
        var friendship = await _friendshipRepository.FindAsync(id);
        if (friendship == null || friendship.UserId != userId)
            throw new InvalidOperationException("好友关系不存在");

        if (!string.IsNullOrEmpty(updateDto.GroupName))
            friendship.GroupName = updateDto.GroupName;
        if (updateDto.Remark != null)
            friendship.Remark = updateDto.Remark;

        friendship.UpdatedAt = DateTime.UtcNow;

        await _friendshipRepository.UpdateAsync(friendship, autoSave: true);

        return _mapper.Map<FriendshipDto>(friendship);
    }

    public async Task<bool> IsFriendAsync(Guid userId1, Guid userId2)
    {
        return await _friendshipRepository.AnyAsync(f => 
            f.UserId == userId1 && f.FriendId == userId2 && f.IsActive);
    }

    public async Task<BlacklistDto> AddToBlacklistAsync(CreateBlacklistDto createDto, Guid userId)
    {
        // 检查是否已经在黑名单中
        if (await IsBlacklistedAsync(userId, createDto.BlockedUserId))
            throw new InvalidOperationException("该用户已在黑名单中");

        // 如果存在好友关系，先删除
        var friendship = await _friendshipRepository.FirstOrDefaultAsync(f => 
            f.UserId == userId && f.FriendId == createDto.BlockedUserId && f.IsActive);
        
        if (friendship != null)
        {
            friendship.IsActive = false;
            friendship.UpdatedAt = DateTime.UtcNow;
            await _friendshipRepository.UpdateAsync(friendship, autoSave: true);
        }

        var blacklist = new Blacklist
        {
            UserId = userId,
            BlockedUserId = createDto.BlockedUserId,
            Reason = createDto.Reason
        };

        await _blacklistRepository.InsertAsync(blacklist, autoSave: true);

        return _mapper.Map<BlacklistDto>(blacklist);
    }

    public async Task<bool> RemoveFromBlacklistAsync(Guid blacklistId, Guid userId)
    {
        var blacklist = await _blacklistRepository.FindAsync(blacklistId);
        if (blacklist == null || blacklist.UserId != userId)
            return false;

        await _blacklistRepository.DeleteAsync(blacklist);
        return true;
    }

    public async Task<IEnumerable<BlacklistDto>> GetBlacklistAsync(Guid userId)
    {
        var blacklists = await _blacklistRepository.GetListAsync(b => b.UserId == userId);
        return _mapper.Map<IEnumerable<BlacklistDto>>(blacklists);
    }

    public async Task<bool> IsBlacklistedAsync(Guid userId, Guid blockedUserId)
    {
        return await _blacklistRepository.AnyAsync(b => 
            b.UserId == userId && b.BlockedUserId == blockedUserId);
    }
}



