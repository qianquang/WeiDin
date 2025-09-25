import { ref, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '@/stores/auth'

export function useSignalR() {
  const connection = ref<signalR.HubConnection | null>(null)
  const isConnected = ref(false)
  const isConnecting = ref(false)

  // 开始连接
  const startConnection = async () => {
    if (connection.value?.state === signalR.HubConnectionState.Connected) {
      return
    }

    try {
      isConnecting.value = true
      
      const authStore = useAuthStore()
      const token = authStore.token

      if (!token) {
        console.warn('用户未登录，跳过SignalR连接')
        return
      }

      // 创建连接
      connection.value = new signalR.HubConnectionBuilder()
        .withUrl('/chatHub', {
          accessTokenFactory: () => token,
          skipNegotiation: true,
          transport: signalR.HttpTransportType.WebSockets,
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // 重连策略：1秒、2秒、5秒、10秒、30秒
            const delays = [1000, 2000, 5000, 10000, 30000]
            return retryContext.previousRetryCount < delays.length 
              ? delays[retryContext.previousRetryCount] 
              : 30000
          }
        })
        .configureLogging(signalR.LogLevel.Information)
        .build()

      // 连接事件
      connection.value.onclose((error) => {
        console.log('SignalR连接关闭:', error)
        isConnected.value = false
      })

      connection.value.onreconnecting((error) => {
        console.log('SignalR重连中:', error)
        isConnected.value = false
      })

      connection.value.onreconnected((connectionId) => {
        console.log('SignalR重连成功:', connectionId)
        isConnected.value = true
      })

      // 开始连接
      await connection.value.start()
      isConnected.value = true
      console.log('SignalR连接成功')
      
    } catch (error) {
      console.error('SignalR连接失败:', error)
      isConnected.value = false
      throw error
    } finally {
      isConnecting.value = false
    }
  }

  // 停止连接
  const stopConnection = async () => {
    if (connection.value) {
      try {
        await connection.value.stop()
        console.log('SignalR连接已停止')
      } catch (error) {
        console.error('停止SignalR连接失败:', error)
      } finally {
        connection.value = null
        isConnected.value = false
      }
    }
  }

  // 加入群组
  const joinGroup = async (groupId: string) => {
    if (connection.value?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.value.invoke('JoinGroup', groupId)
        console.log(`已加入群组: ${groupId}`)
      } catch (error) {
        console.error('加入群组失败:', error)
        throw error
      }
    } else {
      throw new Error('SignalR连接未建立')
    }
  }

  // 离开群组
  const leaveGroup = async (groupId: string) => {
    if (connection.value?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.value.invoke('LeaveGroup', groupId)
        console.log(`已离开群组: ${groupId}`)
      } catch (error) {
        console.error('离开群组失败:', error)
        throw error
      }
    } else {
      throw new Error('SignalR连接未建立')
    }
  }

  // 发送消息到用户
  const sendMessageToUser = async (targetUserId: string, message: string) => {
    if (connection.value?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.value.invoke('SendMessageToUser', targetUserId, message)
      } catch (error) {
        console.error('发送消息失败:', error)
        throw error
      }
    } else {
      throw new Error('SignalR连接未建立')
    }
  }

  // 发送消息到群组
  const sendMessageToGroup = async (groupId: string, message: string) => {
    if (connection.value?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.value.invoke('SendMessageToGroup', groupId, message)
      } catch (error) {
        console.error('发送群组消息失败:', error)
        throw error
      }
    } else {
      throw new Error('SignalR连接未建立')
    }
  }

  // 更新在线状态
  const updateOnlineStatus = async (isOnline: boolean) => {
    if (connection.value?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.value.invoke('UpdateOnlineStatus', isOnline)
      } catch (error) {
        console.error('更新在线状态失败:', error)
        throw error
      }
    } else {
      throw new Error('SignalR连接未建立')
    }
  }

  // 标记消息为已读
  const markMessageAsRead = async (messageId: string, targetUserId: string) => {
    if (connection.value?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.value.invoke('MarkMessageAsRead', messageId, targetUserId)
      } catch (error) {
        console.error('标记消息已读失败:', error)
        throw error
      }
    } else {
      throw new Error('SignalR连接未建立')
    }
  }

  // 标记消息为已送达
  const markMessageAsDelivered = async (messageId: string, targetUserId: string) => {
    if (connection.value?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.value.invoke('MarkMessageAsDelivered', messageId, targetUserId)
      } catch (error) {
        console.error('标记消息已送达失败:', error)
        throw error
      }
    } else {
      throw new Error('SignalR连接未建立')
    }
  }

  // 组件卸载时断开连接
  onUnmounted(() => {
    stopConnection()
  })

  return {
    connection,
    isConnected,
    isConnecting,
    startConnection,
    stopConnection,
    joinGroup,
    leaveGroup,
    sendMessageToUser,
    sendMessageToGroup,
    updateOnlineStatus,
    markMessageAsRead,
    markMessageAsDelivered,
  }
}
