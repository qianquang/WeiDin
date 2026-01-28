using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.Core.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace WeiDin.Application.Services;

public class FriendshipService : ApplicationService, IFriendshipService
{
    private readonly IRepository<Friendship, Guid> _friendshipRepository;
    private readonly IRepository<Blacklist, Guid> _blacklistRepository;
    private readonly IRepository<User, Guid> _userRepository;
    private readonly IMapper _mapper;

    public FriendshipService(IRepository<Friendship, Guid> friendshipRepository,
                             IRepository<Blacklist, Guid> blacklistRepository,
                             IRepository<User, Guid> userRepository,
                             IMapper mapper)
    {
        _friendshipRepository = friendshipRepository;
        _blacklistRepository = blacklistRepository;
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<FriendshipDto?> GetByIdAsync(Guid id)
    {
        var queryable = await _friendshipRepository.GetQueryableAsync();
        var friendship = await queryable
            .Include(f => f.User)
            .Include(f => f.Friend)
            .FirstOrDefaultAsync(f => f.Id == id);
        
        if (friendship == null)
            return null;

        return _mapper.Map<FriendshipDto>(friendship);
    }

    public async Task<IEnumerable<FriendshipDto>> GetByUserIdAsync(Guid userId)
    {
        var queryable = await _friendshipRepository.GetQueryableAsync();
        var friendships = await queryable
            .Include(f => f.User)
            .Include(f => f.Friend)
            .Where(f => f.UserId == userId && f.IsActive)
            .ToListAsync();

        return _mapper.Map<IEnumerable<FriendshipDto>>(friendships);
    }

    public async Task<FriendshipDto> AddFriendAsync(CreateFriendshipDto createDto, Guid userId)
    {
        // 验证用户不能添加自己为好友
        if (userId == createDto.FriendId)
            throw new InvalidOperationException("不能添加自己为好友");

        // 验证目标用户是否存在
        var friendUser = await _userRepository.FindAsync(createDto.FriendId);
        if (friendUser == null)
            throw new InvalidOperationException("目标用户不存在");

        // 检查是否已经是好友（IsActive=true）
        if (await IsFriendAsync(userId, createDto.FriendId))
            throw new InvalidOperationException("已经是好友关系");

        // 检查是否已经发送过申请（IsActive=false）
        var existingRequest = await _friendshipRepository.FirstOrDefaultAsync(f =>
            f.UserId == userId && f.FriendId == createDto.FriendId && !f.IsActive);
        
        if (existingRequest != null)
            throw new InvalidOperationException("已经发送过好友申请，请等待对方处理");

        // 检查是否在黑名单中
        if (await IsBlacklistedAsync(userId, createDto.FriendId))
            throw new InvalidOperationException("该用户已被拉黑");

        // 创建好友申请（IsActive=false）
        var friendship = new Friendship
        {
            UserId = userId,
            FriendId = createDto.FriendId,
            GroupName = createDto.GroupName,
            Remark = createDto.Remark,
            IsActive = false, // 申请状态，等待对方同意
            CreatedAt = DateTime.UtcNow
        };

        await _friendshipRepository.InsertAsync(friendship, autoSave: true);

        // 重新加载以包含导航属性
        var queryable = await _friendshipRepository.GetQueryableAsync();
        var loadedFriendship = await queryable
            .Include(f => f.User)
            .Include(f => f.Friend)
            .FirstOrDefaultAsync(f => f.Id == friendship.Id);

        return _mapper.Map<FriendshipDto>(loadedFriendship ?? friendship);
    }

    public async Task<bool> RemoveFriendAsync(Guid friendshipId, Guid userId)
    {
        var friendship = await _friendshipRepository.FindAsync(friendshipId);
        if (friendship == null || friendship.UserId != userId)
            return false;

        // 删除好友：物理删除记录，允许重新申请
        await _friendshipRepository.DeleteAsync(friendship);

        // 同时删除反向的好友关系
        var reverseFriendship = await _friendshipRepository.FirstOrDefaultAsync(f =>
            f.UserId == friendship.FriendId && f.FriendId == friendship.UserId);
        
        if (reverseFriendship != null)
        {
            await _friendshipRepository.DeleteAsync(reverseFriendship);
        }

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

        // 重新加载以包含导航属性
        var queryable = await _friendshipRepository.GetQueryableAsync();
        var loadedFriendship = await queryable
            .Include(f => f.User)
            .Include(f => f.Friend)
            .FirstOrDefaultAsync(f => f.Id == friendship.Id);

        return _mapper.Map<FriendshipDto>(loadedFriendship ?? friendship);
    }

    public async Task<bool> IsFriendAsync(Guid userId1, Guid userId2)
    {
        return await _friendshipRepository.AnyAsync(f => 
            f.UserId == userId1 && f.FriendId == userId2 && f.IsActive);
    }

    public async Task<BlacklistDto> AddToBlacklistAsync(CreateBlacklistDto createDto, Guid userId)
    {
        // 验证用户不能拉黑自己
        if (userId == createDto.BlockedUserId)
            throw new InvalidOperationException("不能拉黑自己");

        // 验证目标用户是否存在
        var blockedUser = await _userRepository.FindAsync(createDto.BlockedUserId);
        if (blockedUser == null)
            throw new InvalidOperationException("目标用户不存在");

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
            Reason = createDto.Reason,
            CreatedAt = DateTime.UtcNow
        };

        await _blacklistRepository.InsertAsync(blacklist, autoSave: true);

        // 重新加载以包含导航属性
        var queryable = await _blacklistRepository.GetQueryableAsync();
        var loadedBlacklist = await queryable
            .Include(b => b.User)
            .Include(b => b.BlockedUser)
            .FirstOrDefaultAsync(b => b.Id == blacklist.Id);

        return _mapper.Map<BlacklistDto>(loadedBlacklist ?? blacklist);
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
        var queryable = await _blacklistRepository.GetQueryableAsync();
        var blacklists = await queryable
            .Include(b => b.User)
            .Include(b => b.BlockedUser)
            .Where(b => b.UserId == userId)
            .ToListAsync();
        
        return _mapper.Map<IEnumerable<BlacklistDto>>(blacklists);
    }

    public async Task<bool> IsBlacklistedAsync(Guid userId, Guid blockedUserId)
    {
        return await _blacklistRepository.AnyAsync(b => 
            b.UserId == userId && b.BlockedUserId == blockedUserId);
    }

    // 接受好友申请
    public async Task<FriendshipDto> AcceptFriendRequestAsync(Guid friendshipId, Guid userId)
    {
        // 查找待处理的好友申请（必须是发给当前用户的，且 IsActive=false）
        var friendship = await _friendshipRepository.FirstOrDefaultAsync(f =>
            f.Id == friendshipId && f.FriendId == userId && !f.IsActive);

        if (friendship == null)
            throw new InvalidOperationException("好友申请不存在或已被处理");

        // 检查是否已经是好友
        if (await IsFriendAsync(friendship.UserId, friendship.FriendId))
            throw new InvalidOperationException("已经是好友关系");

        // 激活好友关系（从申请者到被申请者）
        friendship.IsActive = true;
        friendship.UpdatedAt = DateTime.UtcNow;

        await _friendshipRepository.UpdateAsync(friendship, autoSave: true);

        // 创建反向的好友关系（从被申请者到申请者），确保双方的好友列表都有数据
        var reverseFriendshipExists = await _friendshipRepository.FirstOrDefaultAsync(f =>
            f.UserId == userId && f.FriendId == friendship.UserId);
        
        if (reverseFriendshipExists == null)
        {
            var reverseFriendship = new Friendship
            {
                UserId = userId, // 被申请者
                FriendId = friendship.UserId, // 申请者
                GroupName = null, // 反向关系不继承分组
                Remark = null, // 反向关系不继承备注
                IsActive = true, // 直接激活
                CreatedAt = DateTime.UtcNow
            };

            await _friendshipRepository.InsertAsync(reverseFriendship, autoSave: true);
        }
        else if (!reverseFriendshipExists.IsActive)
        {
            // 如果存在但未激活，则激活它
            reverseFriendshipExists.IsActive = true;
            reverseFriendshipExists.UpdatedAt = DateTime.UtcNow;
            await _friendshipRepository.UpdateAsync(reverseFriendshipExists, autoSave: true);
        }

        // 重新加载以包含导航属性
        var queryable = await _friendshipRepository.GetQueryableAsync();
        var loadedFriendship = await queryable
            .Include(f => f.User)
            .Include(f => f.Friend)
            .FirstOrDefaultAsync(f => f.Id == friendship.Id);

        return _mapper.Map<FriendshipDto>(loadedFriendship ?? friendship);
    }

    // 拒绝好友申请
    public async Task<bool> RejectFriendRequestAsync(Guid friendshipId, Guid userId)
    {
        // 查找待处理的好友申请（必须是发给当前用户的，且 IsActive=false）
        var friendship = await _friendshipRepository.FirstOrDefaultAsync(f =>
            f.Id == friendshipId && f.FriendId == userId && !f.IsActive);

        if (friendship == null)
            return false;

        // 物理删除申请记录
        await _friendshipRepository.DeleteAsync(friendship);
        return true;
    }

    // 获取待处理的好友申请列表（收到的申请）
    public async Task<IEnumerable<FriendshipDto>> GetPendingRequestsAsync(Guid userId)
    {
        var queryable = await _friendshipRepository.GetQueryableAsync();
        var requests = await queryable
            .Include(f => f.User)
            .Include(f => f.Friend)
            .Where(f => f.FriendId == userId && !f.IsActive)
            .ToListAsync();

        return _mapper.Map<IEnumerable<FriendshipDto>>(requests);
    }

    // 获取已发送的好友申请列表
    public async Task<IEnumerable<FriendshipDto>> GetSentRequestsAsync(Guid userId)
    {
        var queryable = await _friendshipRepository.GetQueryableAsync();
        var requests = await queryable
            .Include(f => f.User)
            .Include(f => f.Friend)
            .Where(f => f.UserId == userId && !f.IsActive)
            .ToListAsync();

        return _mapper.Map<IEnumerable<FriendshipDto>>(requests);
    }
}



