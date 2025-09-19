using WeiDin.Core.Entities;

namespace WeiDin.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<User> Users { get; }
    IRepository<Message> Messages { get; }
    IRepository<MessageStatus> MessageStatuses { get; }
    IRepository<MessageAttachment> MessageAttachments { get; }
    IRepository<Group> Groups { get; }
    IRepository<GroupMember> GroupMembers { get; }
    IRepository<Friendship> Friendships { get; }
    IRepository<Blacklist> Blacklists { get; }
    
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
