using AutoMapper;
using WeiDin.Application.DTOs;
using WeiDin.Core.Entities;

namespace WeiDin.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // User mappings
        CreateMap<User, UserDto>();
        CreateMap<CreateUserDto, User>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
            .ForMember(dest => dest.IsOnline, opt => opt.Ignore())
            .ForMember(dest => dest.LastSeen, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.SentMessages, opt => opt.Ignore())
            .ForMember(dest => dest.ReceivedMessages, opt => opt.Ignore())
            .ForMember(dest => dest.Friendships, opt => opt.Ignore())
            .ForMember(dest => dest.GroupMembers, opt => opt.Ignore())
            .ForMember(dest => dest.BlacklistedBy, opt => opt.Ignore())
            .ForMember(dest => dest.BlacklistedUsers, opt => opt.Ignore());

        // Message mappings（动态分表查询不加载 Sender/Receiver/Group，需容忍 null）
        CreateMap<Message, MessageDto>()
            .ForMember(dest => dest.SenderName, opt => opt.MapFrom(src => src.Sender != null ? src.Sender.Username : ""))
            .ForMember(dest => dest.SenderAvatar, opt => opt.MapFrom(src => src.Sender != null ? src.Sender.Avatar : null))
            .ForMember(dest => dest.ReceiverName, opt => opt.MapFrom(src => src.Receiver != null ? src.Receiver.Username : null))
            .ForMember(dest => dest.GroupName, opt => opt.MapFrom(src => src.Group != null ? src.Group.Name : null))
            .ForMember(dest => dest.Attachments, opt => opt.MapFrom(src => src.Attachments ?? new List<MessageAttachment>()))
            .ForMember(dest => dest.Statuses, opt => opt.MapFrom(src => src.MessageStatuses ?? new List<MessageStatus>()));

        CreateMap<CreateMessageDto, Message>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.SenderId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Sender, opt => opt.Ignore())
            .ForMember(dest => dest.Receiver, opt => opt.Ignore())
            .ForMember(dest => dest.Group, opt => opt.Ignore())
            .ForMember(dest => dest.MessageStatuses, opt => opt.Ignore())
            .ForMember(dest => dest.Attachments, opt => opt.Ignore());

        // MessageAttachment mappings
        CreateMap<MessageAttachment, MessageAttachmentDto>();
        CreateMap<CreateMessageAttachmentDto, MessageAttachment>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.MessageId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Message, opt => opt.Ignore());

        // MessageStatus mappings（动态分表不加载 User 导航，容忍 null）
        CreateMap<MessageStatus, MessageStatusDto>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User != null ? src.User.Username : ""));

        // Group mappings
        CreateMap<Group, GroupDto>()
            .ForMember(dest => dest.OwnerName, opt => opt.MapFrom(src => src.Owner.Username))
            .ForMember(dest => dest.CurrentMembers, opt => opt.MapFrom(src => src.Members.Count(m => m.IsActive)))
            .ForMember(dest => dest.Members, opt => opt.MapFrom(src => src.Members));

        CreateMap<CreateGroupDto, Group>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.OwnerId, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Owner, opt => opt.Ignore())
            .ForMember(dest => dest.Members, opt => opt.Ignore())
            .ForMember(dest => dest.Messages, opt => opt.Ignore());

        // GroupMember mappings
        CreateMap<GroupMember, GroupMemberDto>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.Username))
            .ForMember(dest => dest.UserAvatar, opt => opt.MapFrom(src => src.User.Avatar));

        CreateMap<AddGroupMemberDto, GroupMember>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.GroupId, opt => opt.Ignore())
            .ForMember(dest => dest.Role, opt => opt.Ignore())
            .ForMember(dest => dest.JoinedAt, opt => opt.Ignore())
            .ForMember(dest => dest.LeftAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.Ignore())
            .ForMember(dest => dest.Group, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore());

        // Friendship mappings
        CreateMap<Friendship, FriendshipDto>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.Username))
            .ForMember(dest => dest.UserAvatar, opt => opt.MapFrom(src => src.User.Avatar))
            .ForMember(dest => dest.FriendName, opt => opt.MapFrom(src => src.Friend.Username))
            .ForMember(dest => dest.FriendAvatar, opt => opt.MapFrom(src => src.Friend.Avatar));

        CreateMap<CreateFriendshipDto, Friendship>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore())
            .ForMember(dest => dest.Friend, opt => opt.Ignore());

        // Blacklist mappings
        CreateMap<Blacklist, BlacklistDto>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.Username))
            .ForMember(dest => dest.BlockedUserName, opt => opt.MapFrom(src => src.BlockedUser.Username));

        CreateMap<CreateBlacklistDto, Blacklist>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore())
            .ForMember(dest => dest.BlockedUser, opt => opt.Ignore());
    }
}


