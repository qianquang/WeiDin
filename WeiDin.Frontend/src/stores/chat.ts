import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { 
  Message, 
  ChatSession, 
  CreateMessageDto, 
  SignalRMessage, 
  SignalRGroupMessage,
  UserStatusChange,
  MessageReadNotification,
  MessageDeliveredNotification
} from '@/types'
import { messageApi } from '@/api'
import { useSignalR } from '@/utils/signalr'

export const useChatStore = defineStore('chat', () => {
  // 状态
  const messages = ref<Map<string, Message[]>>(new Map())
  const sessions = ref<ChatSession[]>([])
  const currentSessionId = ref<string | null>(null)
  const isLoading = ref(false)
  const isConnected = ref(false)

  // SignalR连接
  const { connection, startConnection, stopConnection } = useSignalR()

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

  // 初始化SignalR连接
  const initConnection = async () => {
    try {
      await startConnection()
      if (connection.value) {
        isConnected.value = true
        setupSignalRHandlers()
      }
    } catch (error) {
      console.warn('SignalR连接失败，但不影响页面显示:', error)
    }
  }

  // 设置SignalR事件处理器
  const setupSignalRHandlers = () => {
    if (!connection.value) return

    // 接收私聊消息
    connection.value.on('ReceiveMessage', (data: SignalRMessage) => {
      handleReceiveMessage(data)
    })

    // 接收群聊消息
    connection.value.on('ReceiveGroupMessage', (data: SignalRGroupMessage) => {
      handleReceiveGroupMessage(data)
    })

    // 用户状态变化
    connection.value.on('UserStatusChanged', (data: UserStatusChange) => {
      handleUserStatusChange(data)
    })

    // 消息已读
    connection.value.on('MessageRead', (data: MessageReadNotification) => {
      handleMessageRead(data)
    })

    // 消息已送达
    connection.value.on('MessageDelivered', (data: MessageDeliveredNotification) => {
      handleMessageDelivered(data)
    })
  }

  // 处理接收到的私聊消息
  const handleReceiveMessage = (data: SignalRMessage) => {
    // 这里需要根据实际的消息格式来处理
    console.log('收到私聊消息:', data)
  }

  // 处理接收到的群聊消息
  const handleReceiveGroupMessage = (data: SignalRGroupMessage) => {
    console.log('收到群聊消息:', data)
  }

  // 处理用户状态变化
  const handleUserStatusChange = (data: UserStatusChange) => {
    console.log('用户状态变化:', data)
  }

  // 处理消息已读
  const handleMessageRead = (data: MessageReadNotification) => {
    console.log('消息已读:', data)
  }

  // 处理消息已送达
  const handleMessageDelivered = (data: MessageDeliveredNotification) => {
    console.log('消息已送达:', data)
  }

  // 发送消息
  const sendMessage = async (messageData: CreateMessageDto) => {
    try {
      isLoading.value = true
      const message = await messageApi.sendMessage(messageData)
      
      // 添加到消息列表
      addMessage(message)
      
      // 通过SignalR发送
      if (connection.value) {
        if (messageData.receiverId) {
          await connection.value.invoke('SendMessageToUser', messageData.receiverId, message.content)
        } else if (messageData.groupId) {
          await connection.value.invoke('SendMessageToGroup', messageData.groupId, message.content)
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
      
      // 通过SignalR发送已读状态
      if (connection.value) {
        await connection.value.invoke('MarkMessageAsRead', messageId, currentSessionId.value)
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

  // 断开连接
  const disconnect = async () => {
    try {
      await stopConnection()
      isConnected.value = false
    } catch (error) {
      console.error('断开连接失败:', error)
    }
  }

  return {
    // 状态
    messages,
    sessions,
    currentSessionId,
    isLoading,
    isConnected,
    
    // 计算属性
    currentMessages,
    currentSession,
    unreadCount,
    
    // 方法
    initConnection,
    sendMessage,
    loadMessages,
    setCurrentSession,
    addSession,
    removeSession,
    markAsRead,
    searchMessages,
    disconnect,
  }
})
