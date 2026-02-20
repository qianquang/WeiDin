using System.Collections.Generic;
using AutoMapper;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.Core.Entities;
using WeiDin.Core.Inputs;
using WeiDin.Core.Interfaces;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace WeiDin.Application.Services;

public class MessageService : ApplicationService, IMessageService
{
    private readonly IDynamicMessageRepository _dynamicMessageRepository;
    private readonly IRepository<User, Guid> _userRepository;
    private readonly IFriendshipService _friendshipService;
    private readonly IGroupService _groupService;
    private readonly IMapper _mapper;

    public MessageService(
        IDynamicMessageRepository dynamicMessageRepository,
        IRepository<User, Guid> userRepository,
        IFriendshipService friendshipService,
        IGroupService groupService,
        IMapper mapper)
    {
        _dynamicMessageRepository = dynamicMessageRepository;
        _userRepository = userRepository;
        _friendshipService = friendshipService;
        _groupService = groupService;
        _mapper = mapper;
    }

    public async Task<MessageDto?> GetByIdAsync(Guid relationId, Guid id)
    {
        var message = await _dynamicMessageRepository.GetByIdAsync(relationId, id);
        if (message == null) return null;
        return await MapToDtoAsync(message, relationId);
    }

    public async Task<IEnumerable<MessageDto>> GetByRelationIdAsync(Guid relationId, int page = 1, int pageSize = 20)
    {
        var messages = await _dynamicMessageRepository.GetByRelationIdAsync(relationId, page, pageSize);
        var dtos = new List<MessageDto>();
        foreach (var msg in messages)
            dtos.Add(await MapToDtoAsync(msg, relationId));
        return dtos;
    }

    public async Task<MessageDto> SendMessageAsync(CreateMessageDto createMessageDto, Guid senderId)
    {
        var relationId = createMessageDto.RelationId;
        var input = MapToCreateMessageInput(createMessageDto);
        
        // 首先判断 relationId 是否是群组ID（群组的 ConversationId = GroupId）
        var group = await _groupService.GetByIdAsync(relationId);
        if (group != null)
        {
            // 这是群组消息
            // 验证用户是否是群组成员（IsActive = true）
            if (!await _groupService.IsMemberAsync(relationId, senderId))
            {
                throw new InvalidOperationException("您不是该群组的成员，无法发送消息");
            }
            
            // 设置 GroupId
            input.GroupId = relationId;
        }
        else
        {
            // 这是私聊消息，通过 relationId 查询 Friendship 获取接收方ID
            if (!input.ReceiverId.HasValue)
            {
                var friendship = await _friendshipService.GetByConversationIdAsync(relationId, senderId);
                if (friendship != null)
                {
                    // 确定接收方：如果当前用户是 UserId，则接收方是 FriendId，否则接收方是 UserId
                    input.ReceiverId = friendship.UserId == senderId ? friendship.FriendId : friendship.UserId;
                }
                else
                {
                    // 如果查询不到好友关系，抛出异常，避免保存 ReceiverId 为 NULL 的消息
                    throw new InvalidOperationException($"未找到 conversationId={relationId} 对应的好友关系，无法确定接收方");
                }
            }
        }
        
        var message = await _dynamicMessageRepository.SendMessageAsync(relationId, input, senderId);
        
        // 如果是群组消息，为所有群成员创建状态记录（除了发送者）
        if (input.GroupId.HasValue)
        {
            try
            {
                var members = await _groupService.GetMembersAsync(input.GroupId.Value);
                var memberIds = members.Select(m => m.UserId).ToList();
                await _dynamicMessageRepository.CreateGroupMessageStatusesAsync(relationId, message.Id, senderId, memberIds);
            }
            catch (Exception ex)
            {
                // 如果创建成员状态记录失败，记录日志但不影响消息发送
                //Logger.LogWarning(ex, "为群组消息创建成员状态记录失败，MessageId: {MessageId}, GroupId: {GroupId}", message.Id, input.GroupId.Value);
            }
        }
        
        var dto = _mapper.Map<MessageDto>(message);
        dto.RelationId = relationId;

        // 补全发送者信息（SendMessageAsync 返回的 Message 实体没有加载 Sender 导航属性）
        var sender = await _userRepository.FindAsync(senderId);
        if (sender != null)
        {
            dto.SenderName = sender.Nickname ?? sender.Username;
            dto.SenderAvatar = sender.Avatar;
        }

        return dto;
    }

    public async Task<bool> DeleteMessageAsync(Guid relationId, Guid id, Guid userId)
    {
        return await _dynamicMessageRepository.DeleteMessageAsync(relationId, id, userId);
    }

    public async Task<bool> UpdateMessageStatusAsync(Guid relationId, Guid messageId, Guid userId, UpdateMessageStatusDto updateDto)
    {
        // 先尝试正常更新（ReceiverId 不为 NULL 的情况）
        var result = await _dynamicMessageRepository.UpdateMessageStatusAsync(relationId, messageId, userId, updateDto.Status);
        
        // 如果更新失败，可能是 ReceiverId 为 NULL 的旧消息
        if (!result)
        {
            var message = await _dynamicMessageRepository.GetByIdAsync(relationId, messageId);
            if (message != null && !message.ReceiverId.HasValue && !message.GroupId.HasValue && message.SenderId != userId)
            {
                // 通过 RelationId 查询 Friendship 获取接收方信息
                var friendship = await _friendshipService.GetByConversationIdAsync(relationId, userId);
                if (friendship != null)
                {
                    // 确定对方的ID：如果当前用户是 UserId，则对方是 FriendId，否则对方是 UserId
                    var otherUserId = friendship.UserId == userId ? friendship.FriendId : friendship.UserId;
                    
                    // 如果发送方是对方（otherUserId），那么接收方就是当前用户（userId），可以标记
                    if (message.SenderId == otherUserId)
                    {
                        // 直接更新状态（绕过验证），因为我们已经验证了接收方是当前用户
                        result = await _dynamicMessageRepository.UpdateMessageStatusDirectlyAsync(relationId, messageId, userId, updateDto.Status);
                    }
                }
            }
        }
        
        return result;
    }

    public async Task<IEnumerable<MessageDto>> SearchByRelationAsync(Guid relationId, Guid userId, string keyword, int page = 1, int pageSize = 20)
    {
        var messages = await _dynamicMessageRepository.SearchByRelationAsync(relationId, keyword, page, pageSize);
        var dtos = new List<MessageDto>();
        foreach (var msg in messages)
            dtos.Add(await MapToDtoAsync(msg, relationId));
        return dtos;
    }

    public async Task<bool> MarkAsReadAsync(Guid relationId, Guid messageId, Guid userId)
    {
        return await _dynamicMessageRepository.MarkAsReadAsync(relationId, messageId, userId);
    }

    public async Task<bool> MarkAsDeliveredAsync(Guid relationId, Guid messageId, Guid userId)
    {
        return await _dynamicMessageRepository.MarkAsDeliveredAsync(relationId, messageId, userId);
    }

    public async Task<IReadOnlyList<Guid>> MarkAllAsReadAsync(Guid relationId, Guid userId)
    {
        // 1. 标记 ReceiverId 不为 NULL 的消息为已读（正常情况）
        var markedIds = await _dynamicMessageRepository.MarkAllAsReadAsync(relationId, userId);
        var markedIdsList = markedIds.ToList();
        
        // 2. 处理 ReceiverId 为 NULL 的旧消息（修复前保存的消息）
        var nullReceiverMessages = await _dynamicMessageRepository.GetMessagesWithNullReceiverIdAsync(relationId, userId);
        
        if (nullReceiverMessages.Count > 0)
        {
            // 通过 RelationId 查询 Friendship 获取接收方信息
            var friendship = await _friendshipService.GetByConversationIdAsync(relationId, userId);
            if (friendship != null)
            {
                // 确定对方的ID：如果当前用户是 UserId，则对方是 FriendId，否则对方是 UserId
                var otherUserId = friendship.UserId == userId ? friendship.FriendId : friendship.UserId;
                
                // 对于 ReceiverId 为 NULL 的消息，如果发送方是对方（otherUserId），那么接收方就是当前用户（userId）
                // 需要标记这些消息为已读
                var messagesToMark = nullReceiverMessages
                    .Where(m => m.SenderId == otherUserId)
                    .ToList();
                
                foreach (var message in messagesToMark)
                {
                    var result = await _dynamicMessageRepository.MarkAsReadAsync(relationId, message.Id, userId);
                    if (result)
                    {
                        markedIdsList.Add(message.Id);
                    }
                }
            }
        }
        
        return markedIdsList;
    }

    public async Task<int> GetUnreadCountAsync(Guid relationId, Guid userId)
    {
        // 1. 查询 ReceiverId 不为 NULL 的消息未读数量（正常情况）
        var count = await _dynamicMessageRepository.GetUnreadCountAsync(relationId, userId);
        
        // 2. 处理 ReceiverId 为 NULL 的旧消息（修复前保存的消息）
        // 对于私聊消息，通过 RelationId（ConversationId）和 SenderId 推断接收方
        var nullReceiverMessages = await _dynamicMessageRepository.GetMessagesWithNullReceiverIdAsync(relationId, userId);
        
        if (nullReceiverMessages.Count > 0)
        {
            // 通过 RelationId 查询 Friendship 获取接收方信息
            var friendship = await _friendshipService.GetByConversationIdAsync(relationId, userId);
            if (friendship != null)
            {
                // 确定对方的ID：如果当前用户是 UserId，则对方是 FriendId，否则对方是 UserId
                var otherUserId = friendship.UserId == userId ? friendship.FriendId : friendship.UserId;
                
                // 对于 ReceiverId 为 NULL 的消息，需要判断接收方是否是当前用户
                // 如果发送方是对方（otherUserId），那么接收方就是当前用户（userId），应该统计
                var unreadNullReceiverCount = nullReceiverMessages
                    .Count(m => m.SenderId == otherUserId);
                
                count += unreadNullReceiverCount;
            }
        }
        
        return count;
    }

    private Task<MessageDto> MapToDtoAsync(Message message, Guid relationId)
    {
        var dto = _mapper.Map<MessageDto>(message);
        dto.RelationId = relationId;
        return Task.FromResult(dto);
    }

    private static CreateMessageInput MapToCreateMessageInput(CreateMessageDto dto)
    {
        return new CreateMessageInput
        {
            MessageType = dto.MessageType ?? "Text",
            Content = dto.Content,
            Attachments = dto.Attachments?.Select(a => new CreateMessageAttachmentInput
            {
                FileName = a.FileName,
                FilePath = a.FilePath,
                FileType = a.FileType,
                FileSize = a.FileSize,
                ThumbnailPath = a.ThumbnailPath
            }).ToList()
        };
    }
}
