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
      // 注意：后端 MessagesController 在发送消息后会自动通过 SignalR 发送通知
      // 前端无需再调用 SignalR，直接返回消息即可
      return message
    } catch (error) {
      console.error('发送消息失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  const addMessage = (message: Message) => {
    // #region agent log
    fetch('http://127.0.0.1:7242/ingest/0aef303c-291e-44b6-87be-fbb7c9436476',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({location:'chat.ts:54',message:'addMessage调用',data:{messageId:message.id,relationId:message.relationId,currentSessionId:currentSessionId.value},timestamp:Date.now(),sessionId:'debug-session',runId:'run1',hypothesisId:'E'})}).catch(()=>{});
    // #endregion
    
    const rid = message.relationId
    if (!rid) {
      // #region agent log
      fetch('http://127.0.0.1:7242/ingest/0aef303c-291e-44b6-87be-fbb7c9436476',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({location:'chat.ts:57',message:'relationId为空，退出',data:{messageId:message.id},timestamp:Date.now(),sessionId:'debug-session',runId:'run1',hypothesisId:'E'})}).catch(()=>{});
      // #endregion
      return
    }
    if (!messages.value.has(rid)) messages.value.set(rid, [])
    const list = messages.value.get(rid)!
    const i = list.findIndex(m => m.id === message.id)
    if (i >= 0) {
      list[i] = message
      // #region agent log
      fetch('http://127.0.0.1:7242/ingest/0aef303c-291e-44b6-87be-fbb7c9436476',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({location:'chat.ts:63',message:'消息已更新',data:{messageId:message.id,relationId:rid,listLength:list.length},timestamp:Date.now(),sessionId:'debug-session',runId:'run1',hypothesisId:'E'})}).catch(()=>{});
      // #endregion
    } else {
      list.push(message)
      list.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
      // #region agent log
      fetch('http://127.0.0.1:7242/ingest/0aef303c-291e-44b6-87be-fbb7c9436476',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({location:'chat.ts:66',message:'消息已添加',data:{messageId:message.id,relationId:rid,listLength:list.length,isCurrentSession:currentSessionId.value===rid},timestamp:Date.now(),sessionId:'debug-session',runId:'run1',hypothesisId:'E'})}).catch(()=>{});
      // #endregion
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

  const updateSessionLastMessage = (relationId: string, message: Message) => {
    const session = sessions.value.find(s => s.relationId === relationId)
    if (session) {
      session.lastMessage = message
      // 如果当前不在查看该会话，增加未读计数
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
    updateSessionLastMessage,
    markAsRead,
    searchByRelation,
  }
})
