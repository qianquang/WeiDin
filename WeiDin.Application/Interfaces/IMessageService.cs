using WeiDin.Application.DTOs;

namespace WeiDin.Application.Interfaces;

public interface IMessageService
{
    Task<MessageDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<MessageDto>> GetByUserIdAsync(Guid userId, int page = 1, int pageSize = 20);
    Task<IEnumerable<MessageDto>> GetByGroupIdAsync(Guid groupId, int page = 1, int pageSize = 20);
    Task<IEnumerable<MessageDto>> GetConversationAsync(Guid userId1, Guid userId2, int page = 1, int pageSize = 20);
    Task<MessageDto> SendMessageAsync(CreateMessageDto createMessageDto, Guid senderId);
    Task<bool> DeleteMessageAsync(Guid id, Guid userId);
    Task<bool> UpdateMessageStatusAsync(Guid messageId, Guid userId, UpdateMessageStatusDto updateDto);
    Task<IEnumerable<MessageDto>> SearchMessagesAsync(Guid userId, string keyword, int page = 1, int pageSize = 20);
    Task<bool> MarkAsReadAsync(Guid messageId, Guid userId);
    Task<bool> MarkAsDeliveredAsync(Guid messageId, Guid userId);
}
