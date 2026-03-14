// 用户相关类型
export interface User {
  id: string
  username: string
  email: string
  phoneNumber: string
  nickname?: string
  avatar?: string
  bio?: string
  isOnline: boolean
  lastSeen: string
  createdAt: string
}

export interface CreateUserDto {
  username: string
  email: string
  phoneNumber: string
  password: string
  nickname?: string
  bio?: string
}

export interface UpdateUserDto {
  nickname?: string
  avatar?: string
  bio?: string
}

export interface ChangePasswordDto {
  currentPassword: string
  newPassword: string
}

export interface LoginDto {
  username: string
  password: string
}

export interface AuthResponse {
  user: User
  token: string
}

// 消息相关类型
export interface Message {
  id: string
  relationId: string
  senderId: string
  senderName: string
  senderAvatar?: string
  receiverId?: string
  receiverName?: string
  groupId?: string
  groupName?: string
  messageType: 'Text' | 'Image' | 'Video' | 'File'
  content: string
  createdAt: string
  updatedAt?: string
  isDeleted: boolean
  attachments: MessageAttachment[]
  statuses: MessageStatus[]
}

export interface MessageAttachment {
  id: string
  fileName: string
  filePath: string
  fileType: string
  fileSize: number
  thumbnailPath?: string
  createdAt: string
}

export interface MessageStatus {
  id: string
  userId: string
  userName: string
  status: 'Sent' | 'Delivered' | 'Read'
  createdAt: string
}

export interface CreateMessageDto {
  relationId: string
  messageType: 'Text' | 'Image' | 'Video' | 'File'
  content: string
  attachments?: CreateMessageAttachmentDto[]
}

export interface CreateMessageAttachmentDto {
  fileName: string
  filePath: string
  fileType: string
  fileSize: number
  thumbnailPath?: string
}

export interface UpdateMessageStatusDto {
  status: 'Sent' | 'Delivered' | 'Read'
}

// 群组相关类型
export interface Group {
  id: string
  name: string
  description?: string
  avatar?: string
  ownerId: string
  ownerName: string
  announcement?: string
  maxMembers: number
  currentMembers: number
  isActive: boolean
  createdAt: string
  updatedAt: string
  members: GroupMember[]
}

export interface GroupMember {
  id: string
  groupId: string
  groupName?: string  // 用于申请列表显示群组名称
  userId: string
  userName: string
  userAvatar?: string
  role: 'Owner' | 'Admin' | 'Member'
  nickname?: string
  joinedAt: string
  isActive: boolean
}

export interface CreateGroupDto {
  name: string
  description?: string
  avatar?: string
  announcement?: string
  maxMembers: number
}

export interface UpdateGroupDto {
  name?: string
  description?: string
  avatar?: string
  announcement?: string
  maxMembers?: number
}

export interface AddGroupMemberDto {
  userId: string
  nickname?: string
}

export interface UpdateGroupMemberDto {
  nickname?: string
  role?: 'Admin' | 'Member'
}

// 好友相关类型
export interface Friendship {
  id: string
  userId: string
  userName: string
  userAvatar?: string
  friendId: string
  friendName: string
  friendAvatar?: string
  groupName?: string
  remark?: string
  createdAt: string
  isActive: boolean
  /** 会话/关系标识，私聊消息 API 的 relationId */
  conversationId?: string
}

export interface CreateFriendshipDto {
  friendId: string
  groupName?: string
  remark?: string
}

export interface UpdateFriendshipDto {
  groupName?: string
  remark?: string
}

export interface Blacklist {
  id: string
  userId: string
  userName: string
  blockedUserId: string
  blockedUserName: string
  reason?: string
  createdAt: string
}

export interface CreateBlacklistDto {
  blockedUserId: string
  reason?: string
}

// 文件相关类型
export interface FileUploadResult {
  fileName: string
  filePath: string
  fileUrl: string
  fileSize: number
  fileType: string
  uploadedAt: string
  error?: string
}

export interface UploadAndSendFileDto {
  relationId: string
  file: File
  messageType?: 'Image' | 'Video' | 'File'
  content?: string
}

// API响应类型
export interface ApiResponse<T = any> {
  code: number
  message: string
  data: T
}

// 分页类型
export interface PaginationParams {
  page: number
  pageSize: number
}

export interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

// 聊天相关类型
export interface ChatSession {
  id: string
  /** 关系/会话标识，消息 API 的 relationId，用于查改删发 */
  relationId: string
  type: 'private' | 'group'
  name: string
  avatar?: string
  lastMessage?: Message
  unreadCount: number
  isOnline?: boolean
  lastSeen?: string
  /** 私聊时对方的用户 ID，用于关联在线状态 */
  friendId?: string
}

// SignalR消息类型（后端发送的是完整的 MessageDto，与 Message 类型匹配）
export type SignalRMessage = Message

export type SignalRGroupMessage = Message

export interface UserStatusChange {
  userId: string
  isOnline: boolean
  timestamp: string
}

export interface FriendOnlineStatus {
  userId: string
  isOnline: boolean
  lastSeen: string
}

export interface MessageReadNotification {
  messageId: string
  readBy: string
  timestamp: string
}

export interface MessageDeliveredNotification {
  messageId: string
  deliveredTo: string
  timestamp: string
}

// 群组相关 SignalR 事件类型
export interface GroupCreatedNotification extends Group {}

export interface GroupUpdatedNotification extends Group {}

export interface GroupDeletedNotification {
  groupId: string
}

export interface MemberJoinedNotification {
  GroupId: string
  UserId: string
}

export interface MemberLeftNotification {
  GroupId: string
  UserId: string
}

export interface MemberAddedNotification {
  GroupId: string
  UserId: string
}

export interface MemberRemovedNotification {
  GroupId: string
  UserId: string
}

export interface MemberUpdatedNotification {
  GroupId: string
  UserId: string
  UpdateDto: UpdateGroupMemberDto
}

// 群组申请相关 SignalR 事件类型（使用 GroupMember）
export interface GroupRequestReceivedNotification extends GroupMember {}

export interface GroupRequestAcceptedNotification {
  RequestId: string
  GroupId: string
  GroupName?: string
}

export interface GroupRequestRejectedNotification {
  RequestId: string
  GroupId: string
  GroupName?: string
}
