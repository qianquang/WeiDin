using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.Core.Entities;
using Volo.Abp.Domain.Repositories;

namespace WeiDin.Application.Services;

public class MessageService : IMessageService
{
    private readonly IRepository<Message, Guid> _messageRepository;
    private readonly IRepository<MessageStatus, Guid> _messageStatusRepository;
    private readonly IRepository<MessageAttachment, Guid> _messageAttachmentRepository;
    private readonly IMapper _mapper;

    public MessageService(IRepository<Message, Guid> messageRepository,
                          IRepository<MessageStatus, Guid> messageStatusRepository,
                          IRepository<MessageAttachment, Guid> messageAttachmentRepository,
                          IMapper mapper)
    {
        _messageRepository = messageRepository;
        _messageStatusRepository = messageStatusRepository;
        _messageAttachmentRepository = messageAttachmentRepository;
        _mapper = mapper;
    }

    public async Task<MessageDto?> GetByIdAsync(Guid id)
    {
        var message = await _messageRepository.FindAsync(id);
        if (message == null)
            return null;

        // 加载相关数据
        await LoadMessageRelatedData(message);
        return _mapper.Map<MessageDto>(message);
    }

    public async Task<IEnumerable<MessageDto>> GetByUserIdAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        var messages = await _messageRepository.GetListAsync(m => 
            m.SenderId == userId || m.ReceiverId == userId);
        
        var pagedMessages = messages
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        foreach (var message in pagedMessages)
        {
            await LoadMessageRelatedData(message);
        }

        return _mapper.Map<IEnumerable<MessageDto>>(pagedMessages);
    }

    public async Task<IEnumerable<MessageDto>> GetByGroupIdAsync(Guid groupId, int page = 1, int pageSize = 20)
    {
        var messages = await _messageRepository.GetListAsync(m => m.GroupId == groupId);
        
        var pagedMessages = messages
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        foreach (var message in pagedMessages)
        {
            await LoadMessageRelatedData(message);
        }

        return _mapper.Map<IEnumerable<MessageDto>>(pagedMessages);
    }

    public async Task<IEnumerable<MessageDto>> GetConversationAsync(Guid userId1, Guid userId2, int page = 1, int pageSize = 20)
    {
        var messages = await _messageRepository.GetListAsync(m => 
            (m.SenderId == userId1 && m.ReceiverId == userId2) ||
            (m.SenderId == userId2 && m.ReceiverId == userId1));
        
        var pagedMessages = messages
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        foreach (var message in pagedMessages)
        {
            await LoadMessageRelatedData(message);
        }

        return _mapper.Map<IEnumerable<MessageDto>>(pagedMessages);
    }

    public async Task<MessageDto> SendMessageAsync(CreateMessageDto createMessageDto, Guid senderId)
    {
        var message = new Message
        {
            SenderId = senderId,
            ReceiverId = createMessageDto.ReceiverId,
            GroupId = createMessageDto.GroupId,
            MessageType = createMessageDto.MessageType,
            Content = createMessageDto.Content
        };

        await _messageRepository.InsertAsync(message, autoSave: true);

        // 添加消息状态
        var messageStatus = new MessageStatus
        {
            MessageId = message.Id,
            UserId = senderId,
            Status = "Sent"
        };
        await _messageStatusRepository.InsertAsync(messageStatus, autoSave: true);

        // 添加附件
        if (createMessageDto.Attachments != null && createMessageDto.Attachments.Any())
        {
            foreach (var attachmentDto in createMessageDto.Attachments)
            {
                var attachment = new MessageAttachment
                {
                    MessageId = message.Id,
                    FileName = attachmentDto.FileName,
                    FilePath = attachmentDto.FilePath,
                    FileType = attachmentDto.FileType,
                    FileSize = attachmentDto.FileSize,
                    ThumbnailPath = attachmentDto.ThumbnailPath
                };
                await _messageAttachmentRepository.InsertAsync(attachment, autoSave: true);
            }
        }

        // 加载相关数据并返回
        await LoadMessageRelatedData(message);
        return _mapper.Map<MessageDto>(message);
    }

    public async Task<bool> DeleteMessageAsync(Guid id, Guid userId)
    {
        var message = await _messageRepository.FindAsync(id);
        if (message == null || message.SenderId != userId)
            return false;

        message.IsDeleted = true;
        message.DeletedAt = DateTime.UtcNow;
        message.UpdatedAt = DateTime.UtcNow;

        await _messageRepository.UpdateAsync(message, autoSave: true);
        return true;
    }

    public async Task<bool> UpdateMessageStatusAsync(Guid messageId, Guid userId, UpdateMessageStatusDto updateDto)
    {
        var messageStatus = await _messageStatusRepository.FirstOrDefaultAsync(ms => 
            ms.MessageId == messageId && ms.UserId == userId);

        if (messageStatus == null)
        {
            // 创建新的消息状态
            messageStatus = new MessageStatus
            {
                MessageId = messageId,
                UserId = userId,
                Status = updateDto.Status
            };
            await _messageStatusRepository.InsertAsync(messageStatus, autoSave: true);
        }
        else
        {
            messageStatus.Status = updateDto.Status;
            await _messageStatusRepository.UpdateAsync(messageStatus, autoSave: true);
        }
        return true;
    }

    public async Task<IEnumerable<MessageDto>> SearchMessagesAsync(Guid userId, string keyword, int page = 1, int pageSize = 20)
    {
        var messages = await _messageRepository.GetListAsync(m => 
            (m.SenderId == userId || m.ReceiverId == userId) &&
            m.Content.Contains(keyword) &&
            !m.IsDeleted);
        
        var pagedMessages = messages
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        foreach (var message in pagedMessages)
        {
            await LoadMessageRelatedData(message);
        }

        return _mapper.Map<IEnumerable<MessageDto>>(pagedMessages);
    }

    public async Task<bool> MarkAsReadAsync(Guid messageId, Guid userId)
    {
        return await UpdateMessageStatusAsync(messageId, userId, new UpdateMessageStatusDto { Status = "Read" });
    }

    public async Task<bool> MarkAsDeliveredAsync(Guid messageId, Guid userId)
    {
        return await UpdateMessageStatusAsync(messageId, userId, new UpdateMessageStatusDto { Status = "Delivered" });
    }

    private Task LoadMessageRelatedData(Message message)
    {
        // 这里可以添加预加载相关数据的逻辑
        // 由于我们使用的是简单的Repository模式，这里暂时不实现
        // 在实际项目中，可以使用Include方法预加载相关数据
        return Task.CompletedTask;
    }
}

