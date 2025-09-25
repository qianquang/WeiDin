using Microsoft.EntityFrameworkCore.Storage;
using WeiDin.Core.Entities;
using WeiDin.Core.Interfaces;
using WeiDin.Infrastructure.Data;

namespace WeiDin.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly WeiDinDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(WeiDinDbContext context)
    {
        _context = context;
        Users = new Repository<User>(_context);
        Messages = new Repository<Message>(_context);
        MessageStatuses = new Repository<MessageStatus>(_context);
        MessageAttachments = new Repository<MessageAttachment>(_context);
        Groups = new Repository<Group>(_context);
        GroupMembers = new Repository<GroupMember>(_context);
        Friendships = new Repository<Friendship>(_context);
        Blacklists = new Repository<Blacklist>(_context);
    }

    public IRepository<User> Users { get; }
    public IRepository<Message> Messages { get; }
    public IRepository<MessageStatus> MessageStatuses { get; }
    public IRepository<MessageAttachment> MessageAttachments { get; }
    public IRepository<Group> Groups { get; }
    public IRepository<GroupMember> GroupMembers { get; }
    public IRepository<Friendship> Friendships { get; }
    public IRepository<Blacklist> Blacklists { get; }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}



