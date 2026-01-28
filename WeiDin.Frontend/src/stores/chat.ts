import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { 
  Message, 
  ChatSession, 
  CreateMessageDto
} from '@/types'
import { messageApi } from '@/api'
import { useSignalRStore } from './signalr'

export const useChatStore = defineStore('chat', () => {
  // 状态
  const messages = ref<Map<string, Message[]>>(new Map())
  const sessions = ref<ChatSession[]>([])
  const currentSessionId = ref<string | null>(null)
  const isLoading = ref(false)

  // 从 signalrStore 获取连接（用于发送消息）
  const signalrStore = useSignalRStore()
  const connection = computed(() => signalrStore.connection)
  const isConnected = computed(() => signalrStore.isConnected)

  // 计算属性
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

  // 发送消息
  const sendMessage = async (messageData: CreateMessageDto) => {
    try {
      isLoading.value = true
      const message = await messageApi.sendMessage(messageData)
      
      // 添加到消息列表
      addMessage(message)
      
      // 通过SignalR发送（如果连接存在）
      const conn = connection.value
      if (conn && conn.state === 'Connected') {
        try {
          if (messageData.receiverId) {
            await conn.invoke('SendMessageToUser', messageData.receiverId, message.content)
          } else if (messageData.groupId) {
            await conn.invoke('SendMessageToGroup', messageData.groupId, message.content)
          }
        } catch (error) {
          console.warn('通过 SignalR 发送消息失败:', error)
          // 不影响消息发送成功，因为已经通过 API 发送
        }
      }
      
      return message
    } catch (error) {
      console.error('发送消息失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // 添加消息到列表
  const addMessage = (message: Message) => {
    const sessionId = message.groupId || message.receiverId || message.senderId
    if (!sessionId) return

    if (!messages.value.has(sessionId)) {
      messages.value.set(sessionId, [])
    }
    
    const sessionMessages = messages.value.get(sessionId)!
    const existingIndex = sessionMessages.findIndex(m => m.id === message.id)
    
    if (existingIndex >= 0) {
      sessionMessages[existingIndex] = message
    } else {
      sessionMessages.push(message)
      // 按时间排序
      sessionMessages.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
    }
  }

  // 加载消息历史
  const loadMessages = async (sessionId: string, page = 1, pageSize = 20) => {
    try {
      isLoading.value = true
      let messageList: Message[] = []
      
      // 根据会话类型加载不同的消息
      const session = sessions.value.find(s => s.id === sessionId)
      if (session) {
        if (session.type === 'private') {
          // 私聊消息
          const userIds = sessionId.split('-')
          if (userIds.length === 2) {
            messageList = await messageApi.getConversation(userIds[0], userIds[1], { page, pageSize })
          }
        } else if (session.type === 'group') {
          // 群聊消息
          messageList = await messageApi.getGroupMessages(sessionId, { page, pageSize })
        }
      }
      
      // 添加到消息列表
      messageList.forEach(message => addMessage(message))
      
      return messageList
    } catch (error) {
      console.error('加载消息失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // 设置当前会话
  const setCurrentSession = (sessionId: string) => {
    currentSessionId.value = sessionId
    
    // 清除未读数量
    const session = sessions.value.find(s => s.id === sessionId)
    if (session) {
      session.unreadCount = 0
    }
  }

  // 添加会话
  const addSession = (session: ChatSession) => {
    const existingIndex = sessions.value.findIndex(s => s.id === session.id)
    if (existingIndex >= 0) {
      sessions.value[existingIndex] = session
    } else {
      sessions.value.unshift(session)
    }
  }

  // 删除会话
  const removeSession = (sessionId: string) => {
    const index = sessions.value.findIndex(s => s.id === sessionId)
    if (index >= 0) {
      sessions.value.splice(index, 1)
    }
    
    // 删除相关消息
    messages.value.delete(sessionId)
    
    if (currentSessionId.value === sessionId) {
      currentSessionId.value = null
    }
  }

  // 标记消息为已读
  const markAsRead = async (messageId: string) => {
    try {
      await messageApi.markAsRead(messageId)
      
      // 通过SignalR发送已读状态（如果连接存在）
      const conn = connection.value
      if (conn && conn.state === 'Connected') {
        try {
          await conn.invoke('MarkMessageAsRead', messageId, currentSessionId.value)
        } catch (error) {
          console.warn('通过 SignalR 发送已读状态失败:', error)
        }
      }
    } catch (error) {
      console.error('标记已读失败:', error)
    }
  }

  // 搜索消息
  const searchMessages = async (keyword: string, page = 1, pageSize = 20) => {
    try {
      isLoading.value = true
      return await messageApi.searchMessages(keyword, { page, pageSize })
    } catch (error) {
      console.error('搜索消息失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  return {
    // 状态
    messages,
    sessions,
    currentSessionId,
    isLoading,
    
    // 计算属性
    currentMessages,
    currentSession,
    unreadCount,
    isConnected,
    connection,
    
    // 方法
    sendMessage,
    addMessage,
    loadMessages,
    setCurrentSession,
    addSession,
    removeSession,
    markAsRead,
    searchMessages,
  }
})
