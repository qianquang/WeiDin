/**
 * 好友业务 Store
 * 管理好友、好友申请、黑名单等相关状态
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Friendship, CreateFriendshipDto, UpdateFriendshipDto, Blacklist } from '@/types'
import { friendshipApi } from '@/api'

export const useFriendshipStore = defineStore('friendship', () => {
  // 状态
  const friends = ref<Friendship[]>([])
  const pendingRequests = ref<Friendship[]>([])
  const sentRequests = ref<Friendship[]>([])
  const blacklist = ref<Blacklist[]>([])
  const isLoading = ref(false)

  /**
   * 设置待处理的好友申请
   */
  const setPendingRequests = (data: Friendship[]): void => {
    pendingRequests.value = data
  }

  /**
   * 追加待处理的好友申请
   */
  const appendPendingRequest = (data: Friendship): void => {
    // 检查是否已存在
    const existingIndex = pendingRequests.value.findIndex(r => r.id === data.id)
    if (existingIndex >= 0) {
      pendingRequests.value[existingIndex] = data
    } else {
      pendingRequests.value.unshift(data)
    }
  }

  /**
   * 移除已发送的好友申请
   */
  const removeSentRequest = (id: string): void => {
    const index = pendingRequests.value.findIndex(r => r.id === id)
    if (index >= 0) {
      pendingRequests.value.splice(index, 1)
    }
    
    // 也从已发送列表中移除
    const sentIndex = sentRequests.value.findIndex(r => r.id === id)
    if (sentIndex >= 0) {
      sentRequests.value.splice(sentIndex, 1)
    }
  }

  /**
   * 添加好友
   */
  const appendFriend = (data: Friendship): void => {
    // 检查是否已存在
    const existingIndex = friends.value.findIndex(f => f.id === data.id)
    if (existingIndex >= 0) {
      friends.value[existingIndex] = data
    } else {
      friends.value.unshift(data)
    }
  }

  /**
   * 移除好友
   */
  const removeFriend = (id: string): void => {
    const index = friends.value.findIndex(f => f.id === id)
    if (index >= 0) {
      friends.value.splice(index, 1)
    }
  }

  /**
   * 加载好友列表
   */
  const loadFriends = async (): Promise<void> => {
    try {
      isLoading.value = true
      friends.value = await friendshipApi.getFriendships()
    } catch (error) {
      console.error('加载好友列表失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 加载待处理的好友申请
   */
  const loadPendingRequests = async (): Promise<void> => {
    try {
      isLoading.value = true
      pendingRequests.value = await friendshipApi.getPendingRequests()
    } catch (error) {
      console.error('加载待处理申请失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 加载已发送的好友申请
   */
  const loadSentRequests = async (): Promise<void> => {
    try {
      isLoading.value = true
      sentRequests.value = await friendshipApi.getSentRequests()
    } catch (error) {
      console.error('加载已发送申请失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 加载黑名单
   */
  const loadBlacklist = async (): Promise<void> => {
    try {
      isLoading.value = true
      blacklist.value = await friendshipApi.getBlacklist()
    } catch (error) {
      console.error('加载黑名单失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 添加好友
   */
  const addFriend = async (data: CreateFriendshipDto): Promise<Friendship> => {
    try {
      isLoading.value = true
      const friendship = await friendshipApi.addFriend(data)
      // 添加到已发送列表
      sentRequests.value.unshift(friendship)
      return friendship
    } catch (error) {
      console.error('添加好友失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 删除好友
   */
  const deleteFriend = async (id: string): Promise<void> => {
    try {
      isLoading.value = true
      await friendshipApi.removeFriend(id)
      removeFriend(id)
    } catch (error) {
      console.error('删除好友失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 更新好友信息
   */
  const updateFriendship = async (id: string, data: UpdateFriendshipDto): Promise<Friendship> => {
    try {
      isLoading.value = true
      const updated = await friendshipApi.updateFriendship(id, data)
      // 更新好友列表中的对应项
      const index = friends.value.findIndex(f => f.id === id)
      if (index >= 0) {
        friends.value[index] = updated
      }
      return updated
    } catch (error) {
      console.error('更新好友信息失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 接受好友申请
   */
  const acceptFriendRequest = async (id: string): Promise<Friendship> => {
    try {
      isLoading.value = true
      const friendship = await friendshipApi.acceptFriendRequest(id)
      // 从待处理列表中移除
      removeSentRequest(id)
      // 添加到好友列表
      appendFriend(friendship)
      return friendship
    } catch (error) {
      console.error('接受好友申请失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 拒绝好友申请
   */
  const rejectFriendRequest = async (id: string): Promise<void> => {
    try {
      isLoading.value = true
      await friendshipApi.rejectFriendRequest(id)
      // 从待处理列表中移除
      removeSentRequest(id)
    } catch (error) {
      console.error('拒绝好友申请失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 从黑名单移除
   */
  const removeFromBlacklist = async (id: string): Promise<void> => {
    try {
      isLoading.value = true
      await friendshipApi.removeFromBlacklist(id)
      // 从黑名单列表中移除
      const index = blacklist.value.findIndex(b => b.id === id)
      if (index >= 0) {
        blacklist.value.splice(index, 1)
      }
    } catch (error) {
      console.error('从黑名单移除失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  return {
    // 状态
    friends,
    pendingRequests,
    sentRequests,
    blacklist,
    isLoading,
    
    // 方法
    setPendingRequests,
    appendPendingRequest,
    removeSentRequest,
    appendFriend,
    removeFriend,
    loadFriends,
    loadPendingRequests,
    loadSentRequests,
    loadBlacklist,
    addFriend,
    deleteFriend,
    updateFriendship,
    acceptFriendRequest,
    rejectFriendRequest,
    removeFromBlacklist,
  }
})
