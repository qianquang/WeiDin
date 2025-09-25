-- 微钉即时通讯系统数据库初始化脚本
-- 如果使用SQL Server LocalDB，请确保已安装SQL Server LocalDB

-- 创建数据库（如果不存在）
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'WeiDinDb')
BEGIN
    CREATE DATABASE WeiDinDb;
END
GO

USE WeiDinDb;
GO

-- 创建用户表
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [Username] nvarchar(50) NOT NULL UNIQUE,
        [Email] nvarchar(100) NOT NULL UNIQUE,
        [PhoneNumber] nvarchar(20) NOT NULL UNIQUE,
        [PasswordHash] nvarchar(max) NOT NULL,
        [Nickname] nvarchar(100) NULL,
        [Avatar] nvarchar(200) NULL,
        [Bio] nvarchar(500) NULL,
        [IsOnline] bit NOT NULL DEFAULT 0,
        [LastSeen] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL
    );
    
    CREATE INDEX [IX_Users_Username] ON [dbo].[Users] ([Username]);
    CREATE INDEX [IX_Users_Email] ON [dbo].[Users] ([Email]);
    CREATE INDEX [IX_Users_PhoneNumber] ON [dbo].[Users] ([PhoneNumber]);
END
GO

-- 创建群组表
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Groups]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Groups] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Avatar] nvarchar(200) NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [Announcement] nvarchar(1000) NULL,
        [MaxMembers] int NOT NULL DEFAULT 500,
        [IsActive] bit NOT NULL DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        FOREIGN KEY ([OwnerId]) REFERENCES [dbo].[Users] ([Id])
    );
    
    CREATE INDEX [IX_Groups_Name] ON [dbo].[Groups] ([Name]);
END
GO

-- 创建消息表
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Messages]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Messages] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [SenderId] uniqueidentifier NOT NULL,
        [ReceiverId] uniqueidentifier NULL,
        [GroupId] uniqueidentifier NULL,
        [MessageType] nvarchar(50) NOT NULL DEFAULT 'Text',
        [Content] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [DeletedAt] datetime2 NULL,
        FOREIGN KEY ([SenderId]) REFERENCES [dbo].[Users] ([Id]),
        FOREIGN KEY ([ReceiverId]) REFERENCES [dbo].[Users] ([Id]),
        FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Groups] ([Id])
    );
    
    CREATE INDEX [IX_Messages_SenderId] ON [dbo].[Messages] ([SenderId]);
    CREATE INDEX [IX_Messages_ReceiverId] ON [dbo].[Messages] ([ReceiverId]);
    CREATE INDEX [IX_Messages_GroupId] ON [dbo].[Messages] ([GroupId]);
    CREATE INDEX [IX_Messages_CreatedAt] ON [dbo].[Messages] ([CreatedAt]);
END
GO

-- 创建消息状态表
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MessageStatuses]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[MessageStatuses] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [MessageId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT 'Sent',
        [CreatedAt] datetime2 NOT NULL,
        FOREIGN KEY ([MessageId]) REFERENCES [dbo].[Messages] ([Id]) ON DELETE CASCADE,
        FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,
        UNIQUE ([MessageId], [UserId])
    );
END
GO

-- 创建消息附件表
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MessageAttachments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[MessageAttachments] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [MessageId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(200) NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [FileType] nvarchar(50) NOT NULL,
        [FileSize] bigint NOT NULL,
        [ThumbnailPath] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        FOREIGN KEY ([MessageId]) REFERENCES [dbo].[Messages] ([Id]) ON DELETE CASCADE
    );
END
GO

-- 创建群组成员表
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GroupMembers]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[GroupMembers] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [GroupId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Role] nvarchar(20) NOT NULL DEFAULT 'Member',
        [Nickname] nvarchar(50) NULL,
        [JoinedAt] datetime2 NOT NULL,
        [LeftAt] datetime2 NULL,
        [IsActive] bit NOT NULL DEFAULT 1,
        FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Groups] ([Id]) ON DELETE CASCADE,
        FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,
        UNIQUE ([GroupId], [UserId])
    );
END
GO

-- 创建好友关系表
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Friendships]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Friendships] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [UserId] uniqueidentifier NOT NULL,
        [FriendId] uniqueidentifier NOT NULL,
        [GroupName] nvarchar(50) NULL,
        [Remark] nvarchar(50) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsActive] bit NOT NULL DEFAULT 1,
        FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]),
        FOREIGN KEY ([FriendId]) REFERENCES [dbo].[Users] ([Id]),
        UNIQUE ([UserId], [FriendId])
    );
END
GO

-- 创建黑名单表
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Blacklists]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Blacklists] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [UserId] uniqueidentifier NOT NULL,
        [BlockedUserId] uniqueidentifier NOT NULL,
        [Reason] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,
        FOREIGN KEY ([BlockedUserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,
        UNIQUE ([UserId], [BlockedUserId])
    );
END
GO

PRINT '数据库初始化完成！';
