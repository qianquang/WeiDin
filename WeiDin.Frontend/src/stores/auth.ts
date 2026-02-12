import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { User, LoginDto, CreateUserDto, UpdateUserDto, ChangePasswordDto } from '@/types'
import { authApi, userApi } from '@/api'
import { useSignalRStore } from './signalr'

export const useAuthStore = defineStore('auth', () => {
  // 状态
  const user = ref<User | null>(null)
  const token = ref<string | null>(localStorage.getItem('token'))
  const isLoading = ref(false)

  // 计算属性
  const isLoggedIn = computed(() => !!token.value && !!user.value)
  const userId = computed(() => user.value?.id || null)

  // 登录
  const login = async (loginData: LoginDto) => {
    try {
      isLoading.value = true
      const response = await authApi.login(loginData)
      
      user.value = response.user
      token.value = response.token
      
      // 保存到本地存储
      localStorage.setItem('token', response.token)
      localStorage.setItem('user', JSON.stringify(response.user))
      
      // 在线状态由 ChatHub.OnConnectedAsync 在 SignalR 连接建立时自动设置
      // 登录后立即建立 SignalR 连接
      try {
        const signalrStore = useSignalRStore()
        await signalrStore.initConnection()
      } catch (error) {
        console.warn('登录后 SignalR 连接初始化失败，不影响登录:', error)
      }
      
      // 连接成功后更新本地用户状态
      if (user.value) {
        user.value.isOnline = true
        user.value.lastSeen = new Date().toISOString()
        localStorage.setItem('user', JSON.stringify(user.value))
      }
      
      return response
    } catch (error: any) {
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // 注册
  const register = async (registerData: CreateUserDto) => {
    try {
      isLoading.value = true
      const response = await authApi.register(registerData)
      
      user.value = response.user
      token.value = response.token
      
      // 保存到本地存储
      localStorage.setItem('token', response.token)
      localStorage.setItem('user', JSON.stringify(response.user))
      
      // 在线状态由 ChatHub.OnConnectedAsync 在 SignalR 连接建立时自动设置
      // 注册后立即建立 SignalR 连接
      try {
        const signalrStore = useSignalRStore()
        await signalrStore.initConnection()
      } catch (error) {
        console.warn('注册后 SignalR 连接初始化失败，不影响注册:', error)
      }
      
      // 连接成功后更新本地用户状态
      if (user.value) {
        user.value.isOnline = true
        user.value.lastSeen = new Date().toISOString()
        localStorage.setItem('user', JSON.stringify(user.value))
      }
      
      return response
    } catch (error) {
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // 退出登录
  const logout = async () => {
    try {
      // 先断开 SignalR 连接（在清 token 之前）
      try {
        const signalrStore = useSignalRStore()
        await signalrStore.disconnect()
      } catch (error) {
        console.warn('断开 SignalR 连接失败，继续登出流程:', error)
      }

      if (user.value) {
        // 在线状态由 SignalR OnDisconnectedAsync 自动处理，无需额外 HTTP 调用
        // 调用后端退出接口
        await authApi.logout(user.value.id)
      }
    } catch (error) {
      // 退出登录失败，继续清除本地状态
    } finally {
      // 清除本地状态
      user.value = null
      token.value = null
      localStorage.removeItem('token')
      localStorage.removeItem('user')
    }
  }

  // 更新用户信息
  const updateUser = async (updateData: UpdateUserDto) => {
    if (!user.value) throw new Error('用户未登录')
    
    try {
      isLoading.value = true
      const updatedUser = await userApi.updateUser(user.value.id, updateData)
      user.value = updatedUser
      localStorage.setItem('user', JSON.stringify(updatedUser))
      return updatedUser
    } catch (error) {
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // 修改密码
  const changePassword = async (passwordData: ChangePasswordDto) => {
    if (!user.value) throw new Error('用户未登录')
    
    try {
      isLoading.value = true
      await userApi.changePassword(user.value.id, passwordData)
    } catch (error) {
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // 设置在线状态（通过 SignalR 通知服务端，服务端会更新数据库并广播给好友）
  const setOnlineStatus = async (isOnline: boolean) => {
    if (!user.value) return
    
    try {
      const signalrStore = useSignalRStore()
      const conn = signalrStore.connection
      if (conn?.state === 'Connected') {
        await conn.invoke('UpdateOnlineStatus', isOnline)
        user.value.isOnline = isOnline
        user.value.lastSeen = new Date().toISOString()
      }
    } catch (error) {
      // 设置在线状态失败，静默处理
    }
  }

  // 初始化用户信息
  const initUser = async () => {
    const savedUser = localStorage.getItem('user')
    if (savedUser && token.value) {
      try {
        user.value = JSON.parse(savedUser)
        
        // 页面刷新后，从后端重新获取用户信息（包括在线状态）
        if (user.value?.id) {
          try {
            await refreshUser()
          } catch (error) {
            // 如果刷新失败，不影响页面显示，使用本地存储的数据
          }
        }
      } catch (error) {
        logout()
      }
    }
  }

  // 刷新用户信息
  const refreshUser = async () => {
    // 尝试从 user.value 或 localStorage 获取用户 ID
    let userId: string | null = null
    
    if (user.value?.id) {
      userId = user.value.id
    } else {
      // 从 localStorage 读取保存的用户信息
      const savedUser = localStorage.getItem('user')
      if (savedUser) {
        try {
          const parsedUser = JSON.parse(savedUser)
          userId = parsedUser.id
        } catch (error) {
          // 解析失败，忽略
        }
      }
    }
    
    if (!userId || !token.value) {
      return
    }
    
    try {
      const updatedUser = await userApi.getUserById(userId)
      
      if (!updatedUser) {
        throw new Error('用户信息不存在')
      }
      
      user.value = updatedUser
      localStorage.setItem('user', JSON.stringify(updatedUser))
    } catch (error) {
      throw error
    }
  }

  return {
    // 状态
    user,
    token,
    isLoading,
    
    // 计算属性
    isLoggedIn,
    userId,
    
    // 方法
    login,
    register,
    logout,
    updateUser,
    changePassword,
    setOnlineStatus,
    initUser,
    refreshUser,
  }
})
