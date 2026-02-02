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

      const conn = connection.value
      if (conn && conn.state === 'Connected') {
        try {
          await conn.invoke('SendMessageToRelation', messageData.relationId, message.content)
        } catch (e) {
          console.warn('SignalR SendMessageToRelation failed:', e)
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

  const addMessage = (message: Message) => {
    const rid = message.relationId
    if (!rid) return
    if (!messages.value.has(rid)) messages.value.set(rid, [])
    const list = messages.value.get(rid)!
    const i = list.findIndex(m => m.id === message.id)
    if (i >= 0) list[i] = message
    else {
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

  const setCurrentSession = (sessionId: string) => {
    currentSessionId.value = sessionId
    const s = sessions.value.find(x => x.id === sessionId)
    if (s) s.unreadCount = 0
  }

  const addSession = (session: ChatSession) => {
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

  const markAsRead = async (relationId: string, messageId: string) => {
    try {
      await messageApi.markAsRead(relationId, messageId)
      const conn = connection.value
      if (conn?.state === 'Connected') {
        try {
          await conn.invoke('MarkMessageAsRead', messageId, relationId)
        } catch (e) {
          console.warn('SignalR MarkMessageAsRead failed:', e)
        }
      }
    } catch (error) {
      console.error('标记已读失败:', error)
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
    markAsRead,
    searchByRelation,
  }
})
