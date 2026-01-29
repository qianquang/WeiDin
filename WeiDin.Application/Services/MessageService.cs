using AutoMapper;
using Microsoft.EntityFrameworkCore;
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
    private readonly IRepository<Friendship, Guid> _friendshipRepository;
    private readonly IRepository<Group, Guid> _groupRepository;
    private readonly IRepository<GroupMember, Guid> _groupMemberRepository;
    private readonly IMapper _mapper;

    public MessageService(IDynamicMessageRepository dynamicMessageRepository,
                          IRepository<Friendship, Guid> friendshipRepository,
                          IRepository<Group, Guid> groupRepository,
                          IRepository<GroupMember, Guid> groupMemberRepository,
                          IMapper mapper)
    {
        _dynamicMessageRepository = dynamicMessageRepository;
        _friendshipRepository = friendshipRepository;
        _groupRepository = groupRepository;
        _groupMemberRepository = groupMemberRepository;
        _mapper = mapper;
    }

    public async Task<MessageDto?> GetByIdAsync(Guid id)
    {
        var message = await _dynamicMessageRepository.GetByIdAsync(id);
        if (message == null)
            return null;

        return await MapToDtoAsync(message);
    }

    public async Task<IEnumerable<MessageDto>> GetByUserIdAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        // 获取用户的所有会话（好友关系和群组）
        var friendships = await _friendshipRepository.GetListAsync(f => 
            (f.UserId == userId || f.FriendId == userId) && f.IsActive && f.ConversationId.HasValue);
        
        var groupMembers = await _groupMemberRepository.GetListAsync(gm => 
            gm.UserId == userId && gm.IsActive);
        var groupIds = groupMembers.Select(gm => gm.GroupId).Distinct().ToList();
        var groups = await _groupRepository.GetListAsync(g => 
            groupIds.Contains(g.Id) && g.ConversationId.HasValue);

        var conversationIds = friendships.Select(f => f.ConversationId!.Value)
            .Concat(groups.Select(g => g.ConversationId!.Value))
            .Distinct()
            .ToList();

        // 从所有会话中查询消息并合并
        var allMessages = new List<Message>();
        foreach (var convId in conversationIds)
        {
            var messages = await _dynamicMessageRepository.GetByConversationAsync(convId, 1, 1000); // 获取足够多的消息
            allMessages.AddRange(messages.Where(m => m.SenderId == userId || m.ReceiverId == userId));
        }

        var pagedMessages = allMessages
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = new List<MessageDto>();
        foreach (var msg in pagedMessages)
        {
            dtos.Add(await MapToDtoAsync(msg));
        }
        return dtos;
    }

    public async Task<IEnumerable<MessageDto>> GetByGroupIdAsync(Guid groupId, int page = 1, int pageSize = 20)
    {
        var group = await _groupRepository.FindAsync(groupId);
        if (group?.ConversationId == null)
            return Enumerable.Empty<MessageDto>();

        var messages = await _dynamicMessageRepository.GetByConversationAsync(group.ConversationId.Value, page, pageSize);
        var dtos = new List<MessageDto>();
        foreach (var msg in messages)
        {
            dtos.Add(await MapToDtoAsync(msg));
        }
        return dtos;
    }

    public async Task<IEnumerable<MessageDto>> GetConversationAsync(Guid userId1, Guid userId2, int page = 1, int pageSize = 20)
    {
        // 查找好友关系的 ConversationId（任意一条记录都可以）
        var friendship = await _friendshipRepository.FirstOrDefaultAsync(f =>
            ((f.UserId == userId1 && f.FriendId == userId2) ||
             (f.UserId == userId2 && f.FriendId == userId1)) &&
            f.IsActive);
        
        if (friendship?.ConversationId == null)
            return Enumerable.Empty<MessageDto>();

        var messages = await _dynamicMessageRepository.GetByConversationAsync(friendship.ConversationId.Value, page, pageSize);
        var dtos = new List<MessageDto>();
        foreach (var msg in messages)
        {
            dtos.Add(await MapToDtoAsync(msg));
        }
        return dtos;
    }

    public async Task<MessageDto> SendMessageAsync(CreateMessageDto createMessageDto, Guid senderId)
    {
        Guid conversationId;
        
        if (createMessageDto.GroupId.HasValue)
        {
            // 群聊：ConversationId = GroupId
            var group = await _groupRepository.FindAsync(createMessageDto.GroupId.Value);
            if (group == null)
                throw new InvalidOperationException("群组不存在");
            conversationId = group.ConversationId ?? group.Id;
        }
        else if (createMessageDto.ReceiverId.HasValue)
        {
            // 私聊：查找好友关系的 ConversationId
            var friendship = await _friendshipRepository.FirstOrDefaultAsync(f =>
                ((f.UserId == senderId && f.FriendId == createMessageDto.ReceiverId) ||
                 (f.UserId == createMessageDto.ReceiverId && f.FriendId == senderId)) &&
                f.IsActive);
            
            if (friendship?.ConversationId == null)
                throw new InvalidOperationException("好友关系不存在或未激活");
            
            conversationId = friendship.ConversationId.Value;
        }
        else
        {
            throw new InvalidOperationException("必须指定接收者或群组");
        }

        var input = MapToCreateMessageInput(createMessageDto);
        var message = await _dynamicMessageRepository.SendMessageAsync(conversationId, input, senderId);
        return _mapper.Map<MessageDto>(message);
    }

    public async Task<bool> DeleteMessageAsync(Guid id, Guid userId)
    {
        return await _dynamicMessageRepository.DeleteMessageAsync(id, userId);
    }

    public async Task<bool> UpdateMessageStatusAsync(Guid messageId, Guid userId, UpdateMessageStatusDto updateDto)
    {
        return await _dynamicMessageRepository.UpdateMessageStatusAsync(messageId, userId, updateDto.Status);
    }

    public async Task<IEnumerable<MessageDto>> SearchMessagesAsync(Guid userId, string keyword, int page = 1, int pageSize = 20)
    {
        // 获取用户的所有会话
        var friendships = await _friendshipRepository.GetListAsync(f => 
            (f.UserId == userId || f.FriendId == userId) && f.IsActive && f.ConversationId.HasValue);
        
        var groupMembers = await _groupMemberRepository.GetListAsync(gm => 
            gm.UserId == userId && gm.IsActive);
        var groupIds = groupMembers.Select(gm => gm.GroupId).Distinct().ToList();
        var groups = await _groupRepository.GetListAsync(g => 
            groupIds.Contains(g.Id) && g.ConversationId.HasValue);

        var conversationIds = friendships.Select(f => f.ConversationId!.Value)
            .Concat(groups.Select(g => g.ConversationId!.Value))
            .Distinct()
            .ToList();

        // 从所有会话中搜索消息
        var allMessages = new List<Message>();
        foreach (var convId in conversationIds)
        {
            var messages = await _dynamicMessageRepository.GetByConversationAsync(convId, 1, 1000);
            allMessages.AddRange(messages.Where(m => 
                (m.SenderId == userId || m.ReceiverId == userId) &&
                m.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase) &&
                !m.IsDeleted));
        }

        var pagedMessages = allMessages
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = new List<MessageDto>();
        foreach (var msg in pagedMessages)
        {
            dtos.Add(await MapToDtoAsync(msg));
        }
        return dtos;
    }

    public async Task<bool> MarkAsReadAsync(Guid messageId, Guid userId)
    {
        return await _dynamicMessageRepository.MarkAsReadAsync(messageId, userId);
    }

    public async Task<bool> MarkAsDeliveredAsync(Guid messageId, Guid userId)
    {
        return await _dynamicMessageRepository.MarkAsDeliveredAsync(messageId, userId);
    }

    private Task<MessageDto> MapToDtoAsync(Message message)
    {
        var dto = _mapper.Map<MessageDto>(message);
        return Task.FromResult(dto);
    }

    private static CreateMessageInput MapToCreateMessageInput(CreateMessageDto dto)
    {
        return new CreateMessageInput
        {
            ReceiverId = dto.ReceiverId,
            GroupId = dto.GroupId,
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

