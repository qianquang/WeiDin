import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { User, LoginDto, CreateUserDto, UpdateUserDto, ChangePasswordDto } from '@/types'
import { authApi, userApi } from '@/api'

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
      
      // 设置在线状态
      await userApi.setOnlineStatus(response.user.id, true)
      
      return response
    } catch (error) {
      console.error('登录失败:', error)
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
      
      return response
    } catch (error) {
      console.error('注册失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // 退出登录
  const logout = async () => {
    try {
      if (user.value) {
        // 设置离线状态
        await userApi.setOnlineStatus(user.value.id, false)
        // 调用后端退出接口
        await authApi.logout(user.value.id)
      }
    } catch (error) {
      console.error('退出登录失败:', error)
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
      console.error('更新用户信息失败:', error)
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
      console.error('修改密码失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  // 设置在线状态
  const setOnlineStatus = async (isOnline: boolean) => {
    if (!user.value) return
    
    try {
      await userApi.setOnlineStatus(user.value.id, isOnline)
      user.value.isOnline = isOnline
      user.value.lastSeen = new Date().toISOString()
    } catch (error) {
      console.error('设置在线状态失败:', error)
    }
  }

  // 初始化用户信息
  const initUser = () => {
    const savedUser = localStorage.getItem('user')
    if (savedUser && token.value) {
      try {
        user.value = JSON.parse(savedUser)
      } catch (error) {
        console.error('解析用户信息失败:', error)
        logout()
      }
    }
  }

  // 刷新用户信息
  const refreshUser = async () => {
    if (!user.value) return
    
    try {
      const updatedUser = await userApi.getUserById(user.value.id)
      user.value = updatedUser
      localStorage.setItem('user', JSON.stringify(updatedUser))
    } catch (error) {
      console.error('刷新用户信息失败:', error)
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
