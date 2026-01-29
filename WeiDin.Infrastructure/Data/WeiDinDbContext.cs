using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using WeiDin.Core.Entities;

namespace WeiDin.Infrastructure.Data;

public class WeiDinDbContext : AbpDbContext<WeiDinDbContext>
{
    public WeiDinDbContext(DbContextOptions<WeiDinDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<MessageStatus> MessageStatuses { get; set; }
    public DbSet<MessageAttachment> MessageAttachments { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<Friendship> Friendships { get; set; }
    public DbSet<Blacklist> Blacklists { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置User实体
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.PhoneNumber).IsUnique();
        });

        // 配置Message实体
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SenderId);
            entity.HasIndex(e => e.ReceiverId);
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.Sender)
                .WithMany(e => e.SentMessages)
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Receiver)
                .WithMany(e => e.ReceivedMessages)
                .HasForeignKey(e => e.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Group)
                .WithMany(e => e.Messages)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 配置MessageStatus实体
        modelBuilder.Entity<MessageStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MessageId, e.UserId }).IsUnique();

            entity.HasOne(e => e.Message)
                .WithMany(e => e.MessageStatuses)
                .HasForeignKey(e => e.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 配置MessageAttachment实体
        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Message)
                .WithMany(e => e.Attachments)
                .HasForeignKey(e => e.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 配置Group实体
        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.ConversationId);

            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 配置GroupMember实体
        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();

            entity.HasOne(e => e.Group)
                .WithMany(e => e.Members)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(e => e.GroupMembers)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 配置Friendship实体
        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.HasIndex(e => e.ConversationId);
            // 只对 IsActive=true 的记录创建唯一索引，允许 IsActive=false 的多条记录存在
            entity.HasIndex(e => new { e.UserId, e.FriendId })
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            entity.HasOne(e => e.User)
                .WithMany(e => e.Friendships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Friend)
                .WithMany()
                .HasForeignKey(e => e.FriendId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 配置Conversation实体
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever(); // Id 由外部设置（FriendshipId 或 GroupId）
            entity.HasIndex(e => e.RelationId);
            entity.HasIndex(e => e.RelationType);
        });

        // 配置Blacklist实体
        modelBuilder.Entity<Blacklist>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.BlockedUserId }).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany(e => e.BlacklistedBy)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.BlockedUser)
                .WithMany(e => e.BlacklistedUsers)
                .HasForeignKey(e => e.BlockedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
