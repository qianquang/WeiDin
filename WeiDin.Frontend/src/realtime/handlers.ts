/**
 * 实时事件处理器注册
 * 应用启动时调用一次，注册所有 Hub 事件到事件总线
 */

import { realtimeBus } from './bus'
import { useFriendshipStore } from '@/stores/friendship'
import { useChatStore } from '@/stores/chat'
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
  MessageDeliveredNotification
} from '@/types'

/**
 * 注册所有实时事件处理器
 * 应用启动时调用一次，与连接无关
 */
export function registerRealtimeHandlers(): void {
  const friendshipStore = useFriendshipStore()
  const chatStore = useChatStore()
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

  console.log('实时事件处理器已注册')
}
