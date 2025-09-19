using AutoMapper;
using Microsoft.EntityFrameworkCore;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.Core.Entities;
using WeiDin.Core.Interfaces;

namespace WeiDin.Application.Services;

public class MessageService : IMessageService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public MessageService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<MessageDto?> GetByIdAsync(Guid id)
    {
        var message = await _unitOfWork.Messages.GetByIdAsync(id);
        if (message == null)
            return null;

        // 加载相关数据
        await LoadMessageRelatedData(message);
        return _mapper.Map<MessageDto>(message);
    }

    public async Task<IEnumerable<MessageDto>> GetByUserIdAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        var messages = await _unitOfWork.Messages.FindAsync(m => 
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
        var messages = await _unitOfWork.Messages.FindAsync(m => m.GroupId == groupId);
        
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
        var messages = await _unitOfWork.Messages.FindAsync(m => 
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

        await _unitOfWork.Messages.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();

        // 添加消息状态
        var messageStatus = new MessageStatus
        {
            MessageId = message.Id,
            UserId = senderId,
            Status = "Sent"
        };
        await _unitOfWork.MessageStatuses.AddAsync(messageStatus);

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
                await _unitOfWork.MessageAttachments.AddAsync(attachment);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        // 加载相关数据并返回
        await LoadMessageRelatedData(message);
        return _mapper.Map<MessageDto>(message);
    }

    public async Task<bool> DeleteMessageAsync(Guid id, Guid userId)
    {
        var message = await _unitOfWork.Messages.GetByIdAsync(id);
        if (message == null || message.SenderId != userId)
            return false;

        message.IsDeleted = true;
        message.DeletedAt = DateTime.UtcNow;
        message.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Messages.UpdateAsync(message);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateMessageStatusAsync(Guid messageId, Guid userId, UpdateMessageStatusDto updateDto)
    {
        var messageStatus = await _unitOfWork.MessageStatuses.FirstOrDefaultAsync(ms => 
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
            await _unitOfWork.MessageStatuses.AddAsync(messageStatus);
        }
        else
        {
            messageStatus.Status = updateDto.Status;
            await _unitOfWork.MessageStatuses.UpdateAsync(messageStatus);
        }

        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<MessageDto>> SearchMessagesAsync(Guid userId, string keyword, int page = 1, int pageSize = 20)
    {
        var messages = await _unitOfWork.Messages.FindAsync(m => 
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

    private async Task LoadMessageRelatedData(Message message)
    {
        // 这里可以添加预加载相关数据的逻辑
        // 由于我们使用的是简单的Repository模式，这里暂时不实现
        // 在实际项目中，可以使用Include方法预加载相关数据
    }
}
