import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { Message, ChatSession, CreateMessageDto } from '@/types'
import { messageApi } from '@/api'
import { useSignalRStore } from './signalr'

export const useChatStore = defineStore('chat', () => {
  const messages = ref<Map<string, Message[]>>(new Map())
  const sessions = ref<ChatSession[]>([])
  const currentSessionId = ref<string | null>(null)
  const isLoading = ref(false)

  // 缓存早到的在线状态（SignalR 推送可能早于 sessions 创建）
  const pendingOnlineStatuses = ref<Map<string, boolean>>(new Map())

  const signalrStore = useSignalRStore()
  const connection = computed(() => signalrStore.connection)
  const isConnected = computed(() => signalrStore.isConnected)

  const currentMessages = computed(() => {
    if (!currentSessionId.value) return []
    return messages.value.get(currentSessionId.value) || []
  })

  const currentSession = computed(() => {
    if (!currentSessionId.value) return null
    return sessions.value.find(s => s.id === currentSessionId.value) || null
  })

  const unreadCount = computed(() => {
    return sessions.value.reduce((total, session) => total + session.unreadCount, 0)
  })

  const sendMessage = async (messageData: CreateMessageDto) => {
    try {
      isLoading.value = true
      const message = await messageApi.send(messageData)
      addMessage(message)
      // 更新侧边栏会话的最后一条消息
      updateSessionLastMessage(messageData.relationId, message)
      return message
    } catch (error) {
      console.error('发送消息失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  const addMessage = (message: Message) => {
    const rid = message.relationId
    if (!rid) return
    if (!messages.value.has(rid)) messages.value.set(rid, [])
    const list = messages.value.get(rid)!
    const i = list.findIndex(m => m.id === message.id)
    if (i >= 0) {
      list[i] = message
    } else {
      list.push(message)
      list.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
    }
  }

  const loadMessages = async (relationId: string, page = 1, pageSize = 20) => {
    try {
      isLoading.value = true
      const messageList = await messageApi.getByRelationId(relationId, { page, pageSize })
      messageList.forEach(m => addMessage(m))
      return messageList
    } catch (error) {
      console.error('加载消息失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  const setCurrentSession = async (sessionId: string) => {
    currentSessionId.value = sessionId
    const s = sessions.value.find(x => x.id === sessionId)
    if (s) {
      s.unreadCount = 0
      // 自动标记该会话的所有消息为已读（私聊和群聊都支持）
      if (s.relationId) {
        try {
          await markAllAsRead(s.relationId)
        } catch (error) {
          // 静默处理错误，避免影响会话切换
          console.warn('自动标记已读失败:', error)
        }
      }
    }
  }

  const addSession = (session: ChatSession) => {
    // 如果有缓存的在线状态，自动应用到新 session
    if (session.type === 'private' && session.friendId) {
      const cached = pendingOnlineStatuses.value.get(session.friendId)
      if (cached !== undefined) {
        session.isOnline = cached
      }
    }
    const i = sessions.value.findIndex(s => s.id === session.id)
    if (i >= 0) sessions.value[i] = session
    else sessions.value.unshift(session)
  }

  const removeSession = (sessionId: string) => {
    const i = sessions.value.findIndex(s => s.id === sessionId)
    if (i >= 0) sessions.value.splice(i, 1)
    messages.value.delete(sessionId)
    if (currentSessionId.value === sessionId) currentSessionId.value = null
  }

  const updateSessionLastMessage = (relationId: string, message: Message) => {
    const session = sessions.value.find(s => s.relationId === relationId)
    if (session) {
      session.lastMessage = message
      // 如果当前不在查看该会话，增加未读计数（私聊和群聊都支持）
      if (currentSessionId.value !== session.id) {
        session.unreadCount = (session.unreadCount || 0) + 1
      }
    }
  }

  const markAsRead = async (relationId: string, messageId: string) => {
    try {
      await messageApi.markAsRead(relationId, messageId)
      
      // 通过 SignalR 通知消息发送方消息已被读取
      // 需要先获取消息对象以确定发送方 ID
      const messageList = messages.value.get(relationId)
      const message = messageList?.find(m => m.id === messageId)
      
      if (message?.senderId) {
        const conn = connection.value
        if (conn?.state === 'Connected') {
          try {
            // 后端 ChatHub.MarkMessageAsRead 期望参数: (messageId, targetUserId)
            // targetUserId 应该是消息的发送方，因为我们要通知发送方消息已被接收方读取
            await conn.invoke('MarkMessageAsRead', messageId, message.senderId)
          } catch (e) {
            console.warn('SignalR MarkMessageAsRead failed:', e)
          }
        }
      }
    } catch (error) {
      console.error('标记已读失败:', error)
    }
  }

  const markAllAsRead = async (relationId: string) => {
    try {
      // 调用后端API批量标记所有消息为已读
      const markedMessageIds = await messageApi.markAllAsRead(relationId)
      
      // 通过 SignalR 通知所有相关消息的发送方
      const messageList = messages.value.get(relationId)
      if (!messageList || markedMessageIds.length === 0) return
      
      const conn = connection.value
      if (conn?.state === 'Connected') {
        // 遍历已标记的消息ID，找到对应的消息并通知发送方
        for (const messageId of markedMessageIds) {
          const message = messageList.find(m => m.id === messageId)
          if (message?.senderId) {
            try {
              // 后端 ChatHub.MarkMessageAsRead 期望参数: (messageId, targetUserId)
              // targetUserId 应该是消息的发送方，因为我们要通知发送方消息已被接收方读取
              await conn.invoke('MarkMessageAsRead', messageId, message.senderId)
            } catch (e) {
              console.warn(`SignalR MarkMessageAsRead failed for message ${messageId}:`, e)
            }
          }
        }
      }
    } catch (error) {
      console.error('批量标记已读失败:', error)
    }
  }

  const searchByRelation = async (relationId: string, keyword: string, page = 1, pageSize = 20) => {
    try {
      isLoading.value = true
      return await messageApi.searchByRelation(relationId, { keyword, page, pageSize })
    } catch (error) {
      console.error('搜索消息失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  const updateSessionOnlineStatus = (friendId: string, isOnline: boolean) => {
    // 始终缓存最新状态，以便后续创建的 session 能获取到
    pendingOnlineStatuses.value.set(friendId, isOnline)
    // 遍历所有私聊会话，找到与该好友相关的会话并更新在线状态
    for (const session of sessions.value) {
      if (session.type === 'private' && session.friendId === friendId) {
        session.isOnline = isOnline
      }
    }
  }

  const batchUpdateOnlineStatus = (statusList: { userId: string; isOnline: boolean }[]) => {
    // 批量更新好友在线状态（用于连接时一次性设置所有好友状态）
    for (const status of statusList) {
      updateSessionOnlineStatus(status.userId, status.isOnline)
    }
  }

  return {
    messages,
    sessions,
    currentSessionId,
    isLoading,
    currentMessages,
    currentSession,
    unreadCount,
    isConnected,
    connection,
    sendMessage,
    addMessage,
    loadMessages,
    setCurrentSession,
    addSession,
    removeSession,
    updateSessionLastMessage,
    updateSessionOnlineStatus,
    batchUpdateOnlineStatus,
    markAsRead,
    markAllAsRead,
    searchByRelation,
  }
})
