import request from './request'
import type { 
  Message, 
  CreateMessageDto, 
  UpdateMessageStatusDto, 
  PaginationParams,
  PaginatedResponse 
} from '@/types'

// 消息相关API
export const messageApi = {
  // 获取消息详情
  getMessageById: (id: string): Promise<Message> => {
    return request.get(`/messages/${id}`)
  },

  // 获取用户消息
  getUserMessages: (userId: string, params: PaginationParams): Promise<Message[]> => {
    return request.get(`/messages/user/${userId}`, { params })
  },

  // 获取群组消息
  getGroupMessages: (groupId: string, params: PaginationParams): Promise<Message[]> => {
    return request.get(`/messages/group/${groupId}`, { params })
  },

  // 获取对话消息
  getConversation: (userId1: string, userId2: string, params: PaginationParams): Promise<Message[]> => {
    return request.get(`/messages/conversation/${userId1}/${userId2}`, { params })
  },

  // 发送消息
  sendMessage: (data: CreateMessageDto): Promise<Message> => {
    return request.post('/messages', data)
  },

  // 删除消息
  deleteMessage: (id: string): Promise<void> => {
    return request.delete(`/messages/${id}`)
  },

  // 更新消息状态
  updateMessageStatus: (id: string, data: UpdateMessageStatusDto): Promise<void> => {
    return request.put(`/messages/${id}/status`, data)
  },

  // 标记为已读
  markAsRead: (id: string): Promise<void> => {
    return request.post(`/messages/${id}/read`)
  },

  // 标记为已送达
  markAsDelivered: (id: string): Promise<void> => {
    return request.post(`/messages/${id}/delivered`)
  },

  // 搜索消息
  searchMessages: (keyword: string, params: PaginationParams): Promise<Message[]> => {
    return request.get('/messages/search', { 
      params: { keyword, ...params } 
    })
  },
}
