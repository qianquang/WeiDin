using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;
using WeiDin.Application.Interfaces;

namespace WeiDin.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;
    private readonly IFriendshipService _friendshipService;
    private readonly IUserService _userService;
    private readonly IGroupService _groupService;

    /// <summary>
    /// 内存在线用户表：UserId → 该用户所有活跃连接 ID 集合。
    /// 使用 static 保证所有 Hub 实例共享同一份数据。
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, HashSet<string>> _onlineUsers = new();

    /// <summary>
    /// 锁对象，用于保护 _onlineUsers 中 HashSet 的线程安全操作。
    /// </summary>
    private static readonly object _lock = new();

    public ChatHub(ILogger<ChatHub> logger, IFriendshipService friendshipService, IUserService userService, IGroupService groupService)
    {
        _logger = logger;
        _friendshipService = friendshipService;
        _userService = userService;
        _groupService = groupService;
    }

    /// <summary>
    /// 判断某个用户当前是否在线（基于内存字典）。
    /// </summary>
    private static bool IsUserOnline(Guid userId)
    {
        if (_onlineUsers.TryGetValue(userId, out var connections))
        {
            lock (_lock)
            {
                return connections.Count > 0;
            }
        }
        return false;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            var uid = userId.Value;
            var connId = Context.ConnectionId;

            // 1. 加入个人 SignalR 组
            var groupName = $"user_{uid}";
            await Groups.AddToGroupAsync(connId, groupName);

            // 2. 将连接 ID 加入内存在线表
            bool wasOffline;
            lock (_lock)
            {
                var connections = _onlineUsers.GetOrAdd(uid, _ => new HashSet<string>());
                wasOffline = connections.Count == 0;
                connections.Add(connId);
            }

            // 3. 如果是该用户的第一个连接（从离线变为在线），更新数据库并广播
            if (wasOffline)
            {
                try
                {
                    await _userService.SetOnlineStatusAsync(uid, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "更新用户 {UserId} 在线状态失败", uid);
                }

                // 向所有好友广播上线通知
                try
                {
                    var friends = await _friendshipService.GetByUserIdAsync(uid);
                    foreach (var friend in friends)
                    {
                        var friendId = friend.UserId == uid ? friend.FriendId : friend.UserId;
                        await Clients.Group($"user_{friendId}").SendAsync("UserStatusChanged", new
                        {
                            UserId = uid,
                            IsOnline = true,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "广播用户 {UserId} 上线通知失败", uid);
                }
            }

            // 4. 推送好友在线状态列表给当前用户（从内存查询，无需访问数据库）
            try
            {
                var friends = await _friendshipService.GetByUserIdAsync(uid);
                var onlineStatusList = new List<object>();

                foreach (var friend in friends)
                {
                    var friendId = friend.UserId == uid ? friend.FriendId : friend.UserId;
                    onlineStatusList.Add(new
                    {
                        UserId = friendId,
                        IsOnline = IsUserOnline(friendId),
                        LastSeen = DateTime.UtcNow // 内存中不存储 LastSeen，用当前时间代替
                    });
                }

                await Clients.Caller.SendAsync("FriendsOnlineStatusLoaded", onlineStatusList);
                _logger.LogInformation("已推送 {Count} 个好友在线状态给用户 {UserId}（内存查询）", onlineStatusList.Count, uid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "推送好友在线状态给用户 {UserId} 失败", uid);
            }

            // 5. 检查并发送待处理的好友申请
            try
            {
                var pendingRequests = await _friendshipService.GetPendingRequestsAsync(uid);
                if (pendingRequests.Any())
                {
                    await Clients.Caller.SendAsync("PendingFriendRequestsLoaded", pendingRequests);
                    _logger.LogInformation("用户 {UserId} 有 {Count} 个待处理的好友申请", uid, pendingRequests.Count());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取用户 {UserId} 的待处理好友申请失败", uid);
            }

            // 6. 自动加入用户所属的所有群组组
            try
            {
                var groups = await _groupService.GetByUserIdAsync(uid);
                foreach (var group in groups)
                {
                    await Groups.AddToGroupAsync(connId, $"group_{group.Id}");
                }
                _logger.LogInformation("用户 {UserId} 已自动加入 {Count} 个群组组", uid, groups.Count());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "自动加入群组组失败，UserId: {UserId}", uid);
            }

            _logger.LogInformation("用户 {UserId} 已连接，连接ID {ConnectionId}，当前连接数 {Count}",
                uid, connId, _onlineUsers.TryGetValue(uid, out var c) ? c.Count : 0);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            var uid = userId.Value;
            var connId = Context.ConnectionId;

            // 1. 从内存在线表移除该连接
            bool isNowOffline = false;
            lock (_lock)
            {
                if (_onlineUsers.TryGetValue(uid, out var connections))
                {
                    connections.Remove(connId);
                    isNowOffline = connections.Count == 0;
                    // 如果没有连接了，清理该条目
                    if (isNowOffline)
                    {
                        _onlineUsers.TryRemove(uid, out _);
                    }
                }
            }

            // 2. 如果是最后一个连接断开（从在线变为离线），更新数据库并广播
            if (isNowOffline)
            {
                try
                {
                    await _userService.SetOnlineStatusAsync(uid, false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "更新用户 {UserId} 离线状态失败", uid);
                }

                // 向所有好友广播下线通知
                try
                {
                    var friends = await _friendshipService.GetByUserIdAsync(uid);
                    foreach (var friend in friends)
                    {
                        var friendId = friend.UserId == uid ? friend.FriendId : friend.UserId;
                        await Clients.Group($"user_{friendId}").SendAsync("UserStatusChanged", new
                        {
                            UserId = uid,
                            IsOnline = false,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    _logger.LogInformation("已向好友广播用户 {UserId} 下线通知", uid);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "广播用户 {UserId} 下线通知失败", uid);
                }
            }

            // 3. 从个人 SignalR 组移除
            await Groups.RemoveFromGroupAsync(connId, $"user_{uid}");
            _logger.LogInformation("用户 {UserId} 连接 {ConnectionId} 已断开，剩余连接数 {Count}",
                uid, connId, _onlineUsers.TryGetValue(uid, out var remaining) ? remaining.Count : 0);
        }
        await base.OnDisconnectedAsync(exception);
    }

    // 加入群组
    public async Task JoinGroup(string groupId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"group_{groupId}");
        _logger.LogInformation("用户 {UserId} 加入群组 {GroupId}", GetUserId(), groupId);
    }

    // 离开群组
    public async Task LeaveGroup(string groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group_{groupId}");
        _logger.LogInformation("用户 {UserId} 离开群组 {GroupId}", GetUserId(), groupId);
    }

    // 发送消息到指定用户
    public async Task SendMessageToUser(string targetUserId, string message)
    {
        var senderId = GetUserId();
        if (senderId.HasValue)
        {
            await Clients.Group($"user_{targetUserId}").SendAsync("ReceiveMessage", new
            {
                SenderId = senderId.Value,
                Message = message,
                Timestamp = DateTime.UtcNow
            });
            _logger.LogInformation("用户 {SenderId} 向用户 {TargetUserId} 发送消息", senderId, targetUserId);
        }
    }

    // 发送消息到群组
    public async Task SendMessageToGroup(string groupId, string message)
    {
        var senderId = GetUserId();
        if (senderId.HasValue)
        {
            await Clients.Group($"group_{groupId}").SendAsync("ReceiveGroupMessage", new
            {
                SenderId = senderId.Value,
                GroupId = groupId,
                Message = message,
                Timestamp = DateTime.UtcNow
            });
            _logger.LogInformation("用户 {SenderId} 向群组 {GroupId} 发送消息", senderId, groupId);
        }
    }

    /// <summary>
    /// 手动设置在线状态（用户主动切换在线/离线按钮）。
    /// 通知范围：好友列表 ∪ 当前所有在线用户中与该用户有关的人（取并集）。
    /// </summary>
    public async Task UpdateOnlineStatus(bool isOnline)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var uid = userId.Value;

        // 1. 更新数据库
        try
        {
            await _userService.SetOnlineStatusAsync(uid, isOnline);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "手动更新用户 {UserId} 在线状态失败", uid);
        }

        // 2. 收集需要通知的用户：好友列表 ∪ 当前在线用户中连接到 Hub 的人
        var notifyUserIds = new HashSet<Guid>();

        // 2a. 好友列表
        try
        {
            var friends = await _friendshipService.GetByUserIdAsync(uid);
            foreach (var friend in friends)
            {
                var friendId = friend.UserId == uid ? friend.FriendId : friend.UserId;
                notifyUserIds.Add(friendId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取用户 {UserId} 好友列表失败", uid);
        }

        // 2b. 当前所有在线用户（从内存字典获取，O(n) 但避免了数据库查询）
        foreach (var onlineUserId in _onlineUsers.Keys)
        {
            if (onlineUserId != uid)
            {
                notifyUserIds.Add(onlineUserId);
            }
        }

        // 3. 向并集中的所有用户推送状态变化
        var statusPayload = new
        {
            UserId = uid,
            IsOnline = isOnline,
            Timestamp = DateTime.UtcNow
        };

        foreach (var targetId in notifyUserIds)
        {
            await Clients.Group($"user_{targetId}").SendAsync("UserStatusChanged", statusPayload);
        }

        _logger.LogInformation("用户 {UserId} 手动更新在线状态为 {IsOnline}，已通知 {Count} 个用户",
            uid, isOnline, notifyUserIds.Count);
    }

    // 发送消息已读状态
    public async Task MarkMessageAsRead(string messageId, string targetUserId)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Clients.Group($"user_{targetUserId}").SendAsync("MessageRead", new
            {
                MessageId = messageId,
                ReadBy = userId.Value,
                Timestamp = DateTime.UtcNow
            });
            _logger.LogInformation("用户 {UserId} 标记消息 {MessageId} 为已读", userId, messageId);
        }
    }

    // 发送消息已送达状态
    public async Task MarkMessageAsDelivered(string messageId, string targetUserId)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Clients.Group($"user_{targetUserId}").SendAsync("MessageDelivered", new
            {
                MessageId = messageId,
                DeliveredTo = userId.Value,
                Timestamp = DateTime.UtcNow
            });
            _logger.LogInformation("用户 {UserId} 标记消息 {MessageId} 为已送达", userId, messageId);
        }
    }

    /// <summary>
    /// 获取当前在线用户数（可用于监控/调试）。
    /// </summary>
    public static int OnlineUserCount => _onlineUsers.Count;

    /// <summary>
    /// 检查指定用户是否在线（供 Controller 等外部调用）。
    /// </summary>
    public static bool CheckUserOnline(Guid userId) => IsUserOnline(userId);

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }
}
