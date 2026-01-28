<template>
  <div id="app">
    <router-view />
  </div>
</template>

<script setup lang="ts">
import { onMounted, onBeforeUnmount } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useSignalRStore } from '@/stores/signalr'
import { registerRealtimeHandlers } from '@/realtime/handlers'

// 注册全局实时事件处理器（应用启动时调用一次）
registerRealtimeHandlers()

const authStore = useAuthStore()
const signalrStore = useSignalRStore()

// 监听 beforeunload 事件的处理函数（需要在 setup 顶层定义）
const handleBeforeUnload = () => {
  if (signalrStore.isConnected) {
    // 注意：beforeunload 中只能做同步操作，这里尝试断开连接
    // 如果断开是异步的，可能无法完全执行，但服务端会在心跳超时后清理
    signalrStore.disconnect().catch(() => {
      // 忽略断开失败，不影响页面关闭
    })
  }
}

// 组件卸载时清理事件监听器（必须在 setup 顶层注册）
onBeforeUnmount(() => {
  window.removeEventListener('beforeunload', handleBeforeUnload)
})

onMounted(async () => {
  // 初始化用户信息（异步，会从后端刷新在线状态）
  await authStore.initUser()
  
  // 如果用户已登录，初始化 SignalR 连接
  if (authStore.isLoggedIn) {
    try {
      await signalrStore.initConnection()
    } catch (error) {
      // SignalR 连接初始化失败，但不影响页面显示
      console.warn('SignalR 连接初始化失败:', error)
    }
  }

  // 监听 beforeunload 事件（关闭/刷新浏览器时断开连接）
  window.addEventListener('beforeunload', handleBeforeUnload)
})
</script>

<style>
#app {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  height: 100vh;
  margin: 0;
  padding: 0;
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
  padding: 0;
  height: 100vh;
  overflow: hidden;
}
</style>
