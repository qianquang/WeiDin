<template>
  <div id="app">
    <router-view />
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useChatStore } from '@/stores/chat'

const authStore = useAuthStore()
const chatStore = useChatStore()

onMounted(async () => {
  // 初始化用户信息（异步，会从后端刷新在线状态）
  await authStore.initUser()
  
  // 如果用户已登录，初始化聊天连接
  if (authStore.isLoggedIn) {
    try {
      await chatStore.initConnection()
    } catch (error) {
      // 聊天连接初始化失败，但不影响页面显示
    }
  }
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
