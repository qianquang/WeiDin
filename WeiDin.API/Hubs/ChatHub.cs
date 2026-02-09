using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using WeiDin.Application.Interfaces;

namespace WeiDin.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;
    private readonly IFriendshipService _friendshipService;

    public ChatHub(ILogger<ChatHub> logger, IFriendshipService friendshipService)
    {
        _logger = logger;
        _friendshipService = friendshipService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            var groupName = $"user_{userId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("用户 {UserId} 已连接到聊天Hub，已加入组 {GroupName}，连接ID {ConnectionId}", userId, groupName, Context.ConnectionId);
            
            // #region agent log
            _logger.LogInformation("[DEBUG-B] 用户连接SignalR: userId={UserId}, groupName={GroupName}, connectionId={ConnectionId}",
                userId.Value, groupName, Context.ConnectionId);
            // #endregion
            
            // 检查并发送待处理的好友申请
            try
            {
                var pendingRequests = await _friendshipService.GetPendingRequestsAsync(userId.Value);
                if (pendingRequests.Any())
                {
                    await Clients.Caller.SendAsync("PendingFriendRequestsLoaded", pendingRequests);
                    _logger.LogInformation("用户 {UserId} 有 {Count} 个待处理的好友申请", userId, pendingRequests.Count());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取用户 {UserId} 的待处理好友申请失败", userId);
            }
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("用户 {UserId} 已断开聊天Hub连接", userId);
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

    // 发送在线状态更新
    public async Task UpdateOnlineStatus(bool isOnline)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Clients.All.SendAsync("UserStatusChanged", new
            {
                UserId = userId.Value,
                IsOnline = isOnline,
                Timestamp = DateTime.UtcNow
            });
            _logger.LogInformation("用户 {UserId} 更新在线状态为 {IsOnline}", userId, isOnline);
        }
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



