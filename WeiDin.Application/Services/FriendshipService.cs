using AutoMapper;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.Core.Entities;
using WeiDin.Core.Interfaces;

namespace WeiDin.Application.Services;

public class FriendshipService : IFriendshipService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public FriendshipService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<FriendshipDto?> GetByIdAsync(Guid id)
    {
        var friendship = await _unitOfWork.Friendships.GetByIdAsync(id);
        if (friendship == null)
            return null;

        return _mapper.Map<FriendshipDto>(friendship);
    }

    public async Task<IEnumerable<FriendshipDto>> GetByUserIdAsync(Guid userId)
    {
        var friendships = await _unitOfWork.Friendships.FindAsync(f => 
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

        await _unitOfWork.Friendships.AddAsync(friendship);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<FriendshipDto>(friendship);
    }

    public async Task<bool> RemoveFriendAsync(Guid friendshipId, Guid userId)
    {
        var friendship = await _unitOfWork.Friendships.GetByIdAsync(friendshipId);
        if (friendship == null || friendship.UserId != userId)
            return false;

        friendship.IsActive = false;
        friendship.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Friendships.UpdateAsync(friendship);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<FriendshipDto> UpdateAsync(Guid id, UpdateFriendshipDto updateDto, Guid userId)
    {
        var friendship = await _unitOfWork.Friendships.GetByIdAsync(id);
        if (friendship == null || friendship.UserId != userId)
            throw new InvalidOperationException("好友关系不存在");

        if (!string.IsNullOrEmpty(updateDto.GroupName))
            friendship.GroupName = updateDto.GroupName;
        if (updateDto.Remark != null)
            friendship.Remark = updateDto.Remark;

        friendship.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Friendships.UpdateAsync(friendship);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<FriendshipDto>(friendship);
    }

    public async Task<bool> IsFriendAsync(Guid userId1, Guid userId2)
    {
        return await _unitOfWork.Friendships.ExistsAsync(f => 
            f.UserId == userId1 && f.FriendId == userId2 && f.IsActive);
    }

    public async Task<BlacklistDto> AddToBlacklistAsync(CreateBlacklistDto createDto, Guid userId)
    {
        // 检查是否已经在黑名单中
        if (await IsBlacklistedAsync(userId, createDto.BlockedUserId))
            throw new InvalidOperationException("该用户已在黑名单中");

        // 如果存在好友关系，先删除
        var friendship = await _unitOfWork.Friendships.FirstOrDefaultAsync(f => 
            f.UserId == userId && f.FriendId == createDto.BlockedUserId && f.IsActive);
        
        if (friendship != null)
        {
            friendship.IsActive = false;
            friendship.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Friendships.UpdateAsync(friendship);
        }

        var blacklist = new Blacklist
        {
            UserId = userId,
            BlockedUserId = createDto.BlockedUserId,
            Reason = createDto.Reason
        };

        await _unitOfWork.Blacklists.AddAsync(blacklist);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<BlacklistDto>(blacklist);
    }

    public async Task<bool> RemoveFromBlacklistAsync(Guid blacklistId, Guid userId)
    {
        var blacklist = await _unitOfWork.Blacklists.GetByIdAsync(blacklistId);
        if (blacklist == null || blacklist.UserId != userId)
            return false;

        await _unitOfWork.Blacklists.DeleteAsync(blacklist);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<BlacklistDto>> GetBlacklistAsync(Guid userId)
    {
        var blacklists = await _unitOfWork.Blacklists.FindAsync(b => b.UserId == userId);
        return _mapper.Map<IEnumerable<BlacklistDto>>(blacklists);
    }

    public async Task<bool> IsBlacklistedAsync(Guid userId, Guid blockedUserId)
    {
        return await _unitOfWork.Blacklists.ExistsAsync(b => 
            b.UserId == userId && b.BlockedUserId == blockedUserId);
    }
}



