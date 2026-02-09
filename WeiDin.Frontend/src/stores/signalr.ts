/**
 * SignalR 连接管理 Store
 * 负责 SignalR 连接的生命周期管理，与登录状态绑定
 */

import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { useSignalR } from '@/utils/signalr'
import { signalRAdapter } from '@/realtime/adapter'
import * as signalR from '@microsoft/signalr'

export const useSignalRStore = defineStore('signalr', () => {
  // 使用 useSignalR composable
  const { connection, isConnected: _isConnected, isConnecting: _isConnecting, startConnection, stopConnection } = useSignalR()

  // 连接状态（从 useSignalR 获取）
  const isConnected = computed(() => _isConnected.value)
  const isConnecting = computed(() => _isConnecting.value)

  /**
   * 初始化连接
   * 在登录成功后或页面刷新时调用
   */
  const initConnection = async (): Promise<void> => {
    try {
      // 如果已连接，直接返回
      if (connection.value?.state === signalR.HubConnectionState.Connected) {
        console.log('SignalR 连接已存在，跳过初始化')
        return
      }

      // 启动连接
      await startConnection()

      // 连接成功后，将连接附加到适配器
      if (connection.value) {
        // #region agent log
        fetch('http://127.0.0.1:7242/ingest/0aef303c-291e-44b6-87be-fbb7c9436476',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({location:'signalr.ts:36',message:'准备附加适配器',data:{connectionState:connection.value.state,isConnected:_isConnected.value},timestamp:Date.now(),sessionId:'debug-session',runId:'run1',hypothesisId:'B'})}).catch(()=>{});
        // #endregion
        
        signalRAdapter.attach(connection.value)
        console.log('SignalR 连接已初始化并附加到适配器')
        
        // #region agent log
        fetch('http://127.0.0.1:7242/ingest/0aef303c-291e-44b6-87be-fbb7c9436476',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({location:'signalr.ts:40',message:'适配器附加完成',data:{isAttached:signalRAdapter.isAttached(),connectionState:connection.value.state},timestamp:Date.now(),sessionId:'debug-session',runId:'run1',hypothesisId:'B'})}).catch(()=>{});
        // #endregion
      }
    } catch (error) {
      console.error('SignalR 连接初始化失败:', error)
      throw error
    }
  }

  /**
   * 断开连接
   * 在登出或关闭浏览器时调用
   */
  const disconnect = async (): Promise<void> => {
    try {
      // 先从适配器分离
      if (signalRAdapter.isAttached()) {
        signalRAdapter.detach()
      }

      // 停止连接
      await stopConnection()
      console.log('SignalR 连接已断开')
    } catch (error) {
      console.error('断开 SignalR 连接失败:', error)
      // 即使断开失败，也继续执行，避免阻塞登出流程
    }
  }

  return {
    // 状态
    connection,
    isConnected,
    isConnecting,
    
    // 方法
    initConnection,
    disconnect,
  }
})
