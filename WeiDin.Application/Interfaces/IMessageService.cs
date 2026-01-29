using Volo.Abp.Application.Services;
using WeiDin.Application.DTOs;

namespace WeiDin.Application.Interfaces;

/// <summary>
/// 消息服务。所有操作均基于 RelationId 定位分表，进行查、改、删、发。
/// </summary>
public interface IMessageService : IApplicationService
{
    Task<MessageDto?> GetByIdAsync(Guid relationId, Guid id);
    Task<IEnumerable<MessageDto>> GetByRelationIdAsync(Guid relationId, int page = 1, int pageSize = 20);
    Task<MessageDto> SendMessageAsync(CreateMessageDto createMessageDto, Guid senderId);
    Task<bool> DeleteMessageAsync(Guid relationId, Guid id, Guid userId);
    Task<bool> UpdateMessageStatusAsync(Guid relationId, Guid messageId, Guid userId, UpdateMessageStatusDto updateDto);
    Task<IEnumerable<MessageDto>> SearchByRelationAsync(Guid relationId, Guid userId, string keyword, int page = 1, int pageSize = 20);
    Task<bool> MarkAsReadAsync(Guid relationId, Guid messageId, Guid userId);
    Task<bool> MarkAsDeliveredAsync(Guid relationId, Guid messageId, Guid userId);
}
