/**
 * 实时事件处理器注册
 * 应用启动时调用一次，注册所有 Hub 事件到事件总线
 */

import { realtimeBus } from './bus'
import { useFriendshipStore } from '@/stores/friendship'
import { useChatStore } from '@/stores/chat'
import { useGroupStore } from '@/stores/group'
import { useInvalidationStore } from '@/stores/invalidation'
import { useAuthStore } from '@/stores/auth'
import { ElMessage } from 'element-plus'
import type { 
  Friendship, 
  SignalRMessage, 
  SignalRGroupMessage,
  UserStatusChange,
  FriendOnlineStatus,
  MessageReadNotification,
  MessageDeliveredNotification,
  GroupCreatedNotification,
  GroupUpdatedNotification,
  GroupDeletedNotification,
  MemberJoinedNotification,
  MemberLeftNotification,
  MemberAddedNotification,
  MemberRemovedNotification,
  MemberUpdatedNotification,
  GroupRequestReceivedNotification,
  GroupRequestAcceptedNotification,
  GroupRequestRejectedNotification,
  Group
} from '@/types'

/**
 * 注册所有实时事件处理器
 * 应用启动时调用一次，与连接无关
 */
export function registerRealtimeHandlers(): void {
  const friendshipStore = useFriendshipStore()
  const chatStore = useChatStore()
  const groupStore = useGroupStore()
  const invalidationStore = useInvalidationStore()
  const authStore = useAuthStore()

  // ========== 好友相关事件 ==========

  // 连接时加载的待处理好友申请
  realtimeBus.on('PendingFriendRequestsLoaded', (data: Friendship[]) => {
    console.log('收到待处理的好友申请:', data)
    if (data && Array.isArray(data)) {
      friendshipStore.setPendingRequests(data)
      if (data.length > 0) {
        ElMessage.info(`您有 ${data.length} 个待处理的好友申请`)
      }
    }
  })

  // 收到新的好友申请
  realtimeBus.on('FriendRequestReceived', (data: Friendship) => {
    console.log('收到好友申请:', data)
    if (data) {
      invalidationStore.markStale('friends')
      friendshipStore.appendPendingRequest(data)
      ElMessage.info(`${data.userName} 向您发送了好友申请`)
    }
  })

  // 好友申请被接受
  realtimeBus.on('FriendRequestAccepted', (data: { friendshipId: string, friendName: string }) => {
    console.log('好友申请被接受:', data)
    if (data) {
      invalidationStore.markStale('friends')
      friendshipStore.removeSentRequest(data.friendshipId)
      ElMessage.success(`${data.friendName} 接受了您的好友申请`)
    }
  })

  // 好友申请被拒绝
  realtimeBus.on('FriendRequestRejected', (data: { friendshipId: string, friendName: string }) => {
    console.log('好友申请被拒绝:', data)
    if (data) {
      friendshipStore.removeSentRequest(data.friendshipId)
      ElMessage.warning(`${data.friendName} 拒绝了您的好友申请`)
    }
  })

  // 新好友添加（当申请被接受后）
  realtimeBus.on('NewFriendAdded', (data: Friendship) => {
    console.log('新好友添加:', data)
    if (data) {
      invalidationStore.markStale('friends')
      friendshipStore.appendFriend(data)
    }
  })

  // ========== 聊天相关事件 ==========

  // 接收私聊消息
  realtimeBus.on('ReceiveMessage', (data: SignalRMessage) => {
    console.log('收到私聊消息:', data)
    if (data && data.id && data.relationId) {
      // 直接添加消息到 store（SignalRMessage 就是 Message 类型）
      chatStore.addMessage(data)
      
      // 更新会话的最后消息和未读计数
      chatStore.updateSessionLastMessage(data.relationId, data)
      
      // 如果会话不存在，标记失效以便页面重新加载会话列表
      const session = chatStore.sessions.find(s => s.relationId === data.relationId)
      if (!session) {
        invalidationStore.markStale('chat')
      }
    }
  })

  // 接收群聊消息
  realtimeBus.on('ReceiveGroupMessage', (data: SignalRGroupMessage) => {
    console.log('收到群聊消息:', data)
    if (data && data.id && data.relationId) {
      // 直接添加消息到 store（SignalRGroupMessage 就是 Message 类型）
      chatStore.addMessage(data)
      
      // 更新会话的最后消息和未读计数
      chatStore.updateSessionLastMessage(data.relationId, data)
      
      // 如果会话不存在，标记失效以便页面重新加载会话列表
      const session = chatStore.sessions.find(s => s.relationId === data.relationId)
      if (!session) {
        invalidationStore.markStale('chat')
      }
    }
  })

  // 用户状态变化（单个好友上线/下线）
  realtimeBus.on('UserStatusChanged', (data: UserStatusChange) => {
    console.log('用户状态变化:', data)
    if (data && data.userId) {
      chatStore.updateSessionOnlineStatus(data.userId, data.isOnline)
    }
  })

  // 好友在线状态列表（连接时由服务端一次性推送）
  realtimeBus.on('FriendsOnlineStatusLoaded', (data: FriendOnlineStatus[]) => {
    console.log('收到好友在线状态列表:', data)
    if (data && Array.isArray(data)) {
      chatStore.batchUpdateOnlineStatus(data)
    }
  })

  // 消息已读
  realtimeBus.on('MessageRead', (data: MessageReadNotification) => {
    console.log('消息已读:', data)
    if (data) {
      invalidationStore.markStale('chat')
    }
  })

  // 消息已送达
  realtimeBus.on('MessageDelivered', (data: MessageDeliveredNotification) => {
    console.log('消息已送达:', data)
    if (data) {
      invalidationStore.markStale('chat')
    }
  })

  // ========== 群组相关事件 ==========

  // 群组已创建
  realtimeBus.on('GroupCreated', (data: GroupCreatedNotification | Group) => {
    console.log('群组已创建:', data)
    if (data) {
      invalidationStore.markStale('groups')
      groupStore.appendGroup(data as Group)
      ElMessage.success(`群组"${(data as Group).name}"创建成功`)
    }
  })

  // 群组信息更新
  realtimeBus.on('GroupUpdated', (data: GroupUpdatedNotification | Group) => {
    console.log('群组信息更新:', data)
    if (data) {
      invalidationStore.markStale('groups')
      groupStore.updateGroupInList(data as Group)
    }
  })

  // 群组已删除
  realtimeBus.on('GroupDeleted', (data: GroupDeletedNotification | string) => {
    console.log('群组已删除:', data)
    const groupId = typeof data === 'string' ? data : data?.groupId
    if (groupId) {
      invalidationStore.markStale('groups')
      groupStore.removeGroup(groupId)
      ElMessage.warning('群组已删除')
    }
  })

  // 群组成员加入
  realtimeBus.on('MemberJoined', (data: MemberJoinedNotification) => {
    console.log('群组成员加入:', data)
    if (data && data.GroupId && data.UserId) {
      invalidationStore.markStale('groups')
      // 如果是当前用户加入，重新加载我的群组列表
      if (data.UserId === authStore.userId) {
        groupStore.loadMyGroups().catch(err => {
          console.error('重新加载我的群组失败:', err)
        })
      }
    }
  })

  // 群组成员离开
  realtimeBus.on('MemberLeft', (data: MemberLeftNotification) => {
    console.log('群组成员离开:', data)
    if (data && data.GroupId && data.UserId) {
      invalidationStore.markStale('groups')
      // 如果是当前用户离开，从我的群组列表移除
      if (data.UserId === authStore.userId) {
        groupStore.removeGroup(data.GroupId)
      }
    }
  })

  // 群组成员被添加
  realtimeBus.on('MemberAdded', (data: MemberAddedNotification) => {
    console.log('群组成员被添加:', data)
    if (data && data.GroupId && data.UserId) {
      invalidationStore.markStale('groups')
      // 如果是当前用户被添加，重新加载我的群组列表
      if (data.UserId === authStore.userId) {
        groupStore.loadMyGroups().catch(err => {
          console.error('重新加载我的群组失败:', err)
        })
      }
    }
  })

  // 群组成员被移除
  realtimeBus.on('MemberRemoved', (data: MemberRemovedNotification) => {
    console.log('群组成员被移除:', data)
    if (data && data.GroupId && data.UserId) {
      invalidationStore.markStale('groups')
      // 如果是当前用户被移除，从我的群组列表移除
      if (data.UserId === authStore.userId) {
        groupStore.removeGroup(data.GroupId)
        ElMessage.warning('您已被移出群组')
      }
    }
  })

  // 群组成员信息更新
  realtimeBus.on('MemberUpdated', (data: MemberUpdatedNotification) => {
    console.log('群组成员信息更新:', data)
    if (data && data.GroupId) {
      invalidationStore.markStale('groups')
    }
  })

  // ========== 群组申请相关事件 ==========

  // 收到群组申请（群主收到）
  realtimeBus.on('GroupRequestReceived', (data: GroupRequestReceivedNotification) => {
    console.log('收到群组申请:', data)
    if (data) {
      groupStore.appendPendingRequest(data)
      ElMessage.info(`收到加入群组"${data.groupName || '未知群组'}"的申请`)
    }
  })

  // 群组申请被接受（申请者收到）
  realtimeBus.on('GroupRequestAccepted', (data: GroupRequestAcceptedNotification) => {
    console.log('群组申请被接受:', data)
    if (data) {
      // 从已发送申请列表中移除
      // 注意：后端返回的是 RequestId，我们需要找到对应的申请并移除
      // 由于我们不知道 requestId，我们可以重新加载已发送申请列表
      groupStore.loadSentRequests().catch(err => {
        console.error('重新加载已发送申请失败:', err)
      })
      // 重新加载我的群组列表（因为已加入）
      groupStore.loadMyGroups().catch(err => {
        console.error('重新加载我的群组失败:', err)
      })
      ElMessage.success(`您的加入群组"${data.GroupName}"的申请已被接受`)
    }
  })

  // 群组申请被拒绝（申请者收到）
  realtimeBus.on('GroupRequestRejected', (data: GroupRequestRejectedNotification) => {
    console.log('群组申请被拒绝:', data)
    if (data) {
      // 从已发送申请列表中移除
      // 由于我们不知道 requestId，我们可以重新加载已发送申请列表
      groupStore.loadSentRequests().catch(err => {
        console.error('重新加载已发送申请失败:', err)
      })
      ElMessage.warning(`您的加入群组"${data.GroupName}"的申请已被拒绝`)
    }
  })

  console.log('实时事件处理器已注册')
}
