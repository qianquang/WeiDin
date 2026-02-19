/**
 * 群组业务 Store
 * 管理群组列表、我的群组等相关状态
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Group, CreateGroupDto, UpdateGroupDto, GroupMember } from '@/types'
import { groupApi } from '@/api'

export const useGroupStore = defineStore('group', () => {
  // 状态
  const myGroups = ref<Group[]>([])
  const allGroups = ref<Group[]>([])
  const isLoading = ref(false)
  
  // 申请相关状态（使用 GroupMember，IsActive=false 表示待处理申请）
  const pendingRequests = ref<GroupMember[]>([])  // 群主收到的待处理申请
  const sentRequests = ref<GroupMember[]>([])  // 用户已发送的申请

  /**
   * 加载我的群组列表
   */
  const loadMyGroups = async (): Promise<void> => {
    try {
      isLoading.value = true
      myGroups.value = await groupApi.getMyGroups()
    } catch (error) {
      console.error('加载我的群组列表失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 加载所有群组列表
   */
  const loadAllGroups = async (page = 1, pageSize = 20): Promise<void> => {
    try {
      isLoading.value = true
      allGroups.value = await groupApi.getAllGroups({ page, pageSize })
    } catch (error) {
      console.error('加载群组列表失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 创建群组
   */
  const createGroup = async (data: CreateGroupDto): Promise<Group> => {
    try {
      isLoading.value = true
      const group = await groupApi.createGroup(data)
      // 添加到我的群组列表
      appendGroup(group)
      return group
    } catch (error) {
      console.error('创建群组失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 更新群组信息
   */
  const updateGroup = async (id: string, data: UpdateGroupDto): Promise<Group> => {
    try {
      isLoading.value = true
      const updated = await groupApi.updateGroup(id, data)
      // 更新群组列表中的对应项
      updateGroupInList(updated)
      return updated
    } catch (error) {
      console.error('更新群组信息失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 删除群组
   */
  const deleteGroup = async (id: string): Promise<void> => {
    try {
      isLoading.value = true
      await groupApi.deleteGroup(id)
      removeGroup(id)
    } catch (error) {
      console.error('删除群组失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 加入群组
   */
  const joinGroup = async (id: string): Promise<void> => {
    try {
      isLoading.value = true
      await groupApi.joinGroup(id)
      // 重新加载我的群组列表
      await loadMyGroups()
    } catch (error) {
      console.error('加入群组失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 退出群组
   */
  const leaveGroup = async (id: string): Promise<void> => {
    try {
      isLoading.value = true
      await groupApi.leaveGroup(id)
      removeGroup(id)
    } catch (error) {
      console.error('退出群组失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 追加群组到列表
   */
  const appendGroup = (data: Group): void => {
    // 检查是否已存在
    const existingIndex = myGroups.value.findIndex(g => g.id === data.id)
    if (existingIndex >= 0) {
      myGroups.value[existingIndex] = data
    } else {
      myGroups.value.unshift(data)
    }
  }

  /**
   * 从列表中移除群组
   */
  const removeGroup = (id: string): void => {
    const index = myGroups.value.findIndex(g => g.id === id)
    if (index >= 0) {
      myGroups.value.splice(index, 1)
    }
    const allIndex = allGroups.value.findIndex(g => g.id === id)
    if (allIndex >= 0) {
      allGroups.value.splice(allIndex, 1)
    }
  }

  /**
   * 更新列表中的群组
   */
  const updateGroupInList = (data: Group): void => {
    const myIndex = myGroups.value.findIndex(g => g.id === data.id)
    if (myIndex >= 0) {
      myGroups.value[myIndex] = data
    }
    const allIndex = allGroups.value.findIndex(g => g.id === data.id)
    if (allIndex >= 0) {
      allGroups.value[allIndex] = data
    }
  }

  // ========== 群组申请相关方法 ==========

  /**
   * 申请加入群组
   */
  const requestJoinGroup = async (groupId: string): Promise<GroupMember> => {
    try {
      isLoading.value = true
      const member = await groupApi.requestJoinGroup(groupId)
      // 添加到已发送申请列表
      appendSentRequest(member)
      return member
    } catch (error) {
      console.error('申请加入群组失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 接受群组申请（仅群主）
   */
  const acceptGroupRequest = async (memberId: string): Promise<GroupMember> => {
    try {
      isLoading.value = true
      const member = await groupApi.acceptGroupRequest(memberId)
      // 从待处理申请列表中移除
      removePendingRequest(memberId)
      // 重新加载我的群组列表（因为新成员已加入）
      await loadMyGroups()
      return member
    } catch (error) {
      console.error('接受群组申请失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 拒绝群组申请（仅群主）
   */
  const rejectGroupRequest = async (memberId: string): Promise<void> => {
    try {
      isLoading.value = true
      await groupApi.rejectGroupRequest(memberId)
      // 从待处理申请列表中移除
      removePendingRequest(memberId)
    } catch (error) {
      console.error('拒绝群组申请失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 加载群组的待处理申请（仅群主）
   */
  const loadPendingRequests = async (groupId: string): Promise<void> => {
    try {
      isLoading.value = true
      pendingRequests.value = await groupApi.getPendingRequests(groupId)
    } catch (error) {
      console.error('加载待处理申请失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 加载已发送的申请
   */
  const loadSentRequests = async (): Promise<void> => {
    try {
      isLoading.value = true
      sentRequests.value = await groupApi.getSentRequests()
    } catch (error) {
      console.error('加载已发送申请失败:', error)
      throw error
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 追加待处理申请
   */
  const appendPendingRequest = (data: GroupMember): void => {
    const existingIndex = pendingRequests.value.findIndex(r => r.id === data.id)
    if (existingIndex >= 0) {
      pendingRequests.value[existingIndex] = data
    } else {
      pendingRequests.value.unshift(data)
    }
  }

  /**
   * 移除待处理申请
   */
  const removePendingRequest = (memberId: string): void => {
    const index = pendingRequests.value.findIndex(r => r.id === memberId)
    if (index >= 0) {
      pendingRequests.value.splice(index, 1)
    }
  }

  /**
   * 追加已发送申请
   */
  const appendSentRequest = (data: GroupMember): void => {
    const existingIndex = sentRequests.value.findIndex(r => r.id === data.id)
    if (existingIndex >= 0) {
      sentRequests.value[existingIndex] = data
    } else {
      sentRequests.value.unshift(data)
    }
  }

  /**
   * 移除已发送申请
   */
  const removeSentRequest = (memberId: string): void => {
    const index = sentRequests.value.findIndex(r => r.id === memberId)
    if (index >= 0) {
      sentRequests.value.splice(index, 1)
    }
  }

  return {
    // 状态
    myGroups,
    allGroups,
    isLoading,
    pendingRequests,
    sentRequests,
    
    // 方法
    loadMyGroups,
    loadAllGroups,
    createGroup,
    updateGroup,
    deleteGroup,
    joinGroup,
    leaveGroup,
    appendGroup,
    removeGroup,
    updateGroupInList,
    
    // 申请相关方法
    requestJoinGroup,
    acceptGroupRequest,
    rejectGroupRequest,
    loadPendingRequests,
    loadSentRequests,
    appendPendingRequest,
    removePendingRequest,
    appendSentRequest,
    removeSentRequest,
  }
})

