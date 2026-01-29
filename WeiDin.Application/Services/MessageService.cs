using AutoMapper;
using WeiDin.Application.DTOs;
using WeiDin.Application.Interfaces;
using WeiDin.Core.Entities;
using WeiDin.Core.Inputs;
using WeiDin.Core.Interfaces;
using Volo.Abp.Application.Services;

namespace WeiDin.Application.Services;

public class MessageService : ApplicationService, IMessageService
{
    private readonly IDynamicMessageRepository _dynamicMessageRepository;
    private readonly IMapper _mapper;

    public MessageService(IDynamicMessageRepository dynamicMessageRepository, IMapper mapper)
    {
        _dynamicMessageRepository = dynamicMessageRepository;
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
        var message = await _dynamicMessageRepository.SendMessageAsync(relationId, input, senderId);
        var dto = _mapper.Map<MessageDto>(message);
        dto.RelationId = relationId;
        return dto;
    }

    public async Task<bool> DeleteMessageAsync(Guid relationId, Guid id, Guid userId)
    {
        return await _dynamicMessageRepository.DeleteMessageAsync(relationId, id, userId);
    }

    public async Task<bool> UpdateMessageStatusAsync(Guid relationId, Guid messageId, Guid userId, UpdateMessageStatusDto updateDto)
    {
        return await _dynamicMessageRepository.UpdateMessageStatusAsync(relationId, messageId, userId, updateDto.Status);
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
