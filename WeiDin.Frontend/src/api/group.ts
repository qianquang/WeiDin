import request from './request'
import type { 
  Group, 
  CreateGroupDto, 
  UpdateGroupDto, 
  GroupMember, 
  AddGroupMemberDto, 
  UpdateGroupMemberDto,
  PaginationParams
} from '@/types'

// 群组相关API
export const groupApi = {
  // 获取所有群组
  getAllGroups: (params: PaginationParams): Promise<Group[]> => {
    return request.get('/groups', { params })
  },

  // 获取群组详情
  getGroupById: (id: string): Promise<Group> => {
    return request.get(`/groups/${id}`)
  },

  // 获取用户群组
  getUserGroups: (userId: string): Promise<Group[]> => {
    return request.get(`/groups/user/${userId}`)
  },

  // 获取当前用户的群组
  getMyGroups: (): Promise<Group[]> => {
    return request.get('/groups/my-groups')
  },

  // 创建群组
  createGroup: (data: CreateGroupDto): Promise<Group> => {
    return request.post('/groups', data)
  },

  // 更新群组信息
  updateGroup: (id: string, data: UpdateGroupDto): Promise<Group> => {
    return request.put(`/groups/${id}`, data)
  },

  // 删除群组
  deleteGroup: (id: string): Promise<void> => {
    return request.delete(`/groups/${id}`)
  },

  // 加入群组
  joinGroup: (id: string): Promise<void> => {
    return request.post(`/groups/${id}/join`)
  },

  // 退出群组
  leaveGroup: (id: string): Promise<void> => {
    return request.post(`/groups/${id}/leave`)
  },

  // 添加群成员
  addMember: (id: string, data: AddGroupMemberDto): Promise<void> => {
    return request.post(`/groups/${id}/members`, data)
  },

  // 移除群成员
  removeMember: (id: string, memberId: string): Promise<void> => {
    return request.delete(`/groups/${id}/members/${memberId}`)
  },

  // 更新群成员信息
  updateMember: (id: string, memberId: string, data: UpdateGroupMemberDto): Promise<void> => {
    return request.put(`/groups/${id}/members/${memberId}`, data)
  },

  // 获取群成员列表
  getGroupMembers: (id: string): Promise<GroupMember[]> => {
    return request.get(`/groups/${id}/members`)
  },

  // ========== 群组申请相关 API（使用 GroupMember，IsActive=false 表示待处理申请） ==========

  // 申请加入群组
  requestJoinGroup: (groupId: string): Promise<GroupMember> => {
    return request.post(`/groups/${groupId}/request`)
  },

  // 接受群组申请（仅群主）
  acceptGroupRequest: (memberId: string): Promise<GroupMember> => {
    return request.post(`/groups/members/${memberId}/accept`)
  },

  // 拒绝群组申请（仅群主）
  rejectGroupRequest: (memberId: string): Promise<void> => {
    return request.post(`/groups/members/${memberId}/reject`)
  },

  // 获取群组的待处理申请（仅群主）
  getPendingRequests: (groupId: string): Promise<GroupMember[]> => {
    return request.get(`/groups/${groupId}/requests/pending`)
  },

  // 获取当前用户已发送的群组申请
  getSentRequests: (): Promise<GroupMember[]> => {
    return request.get('/groups/requests/sent')
  },
}
