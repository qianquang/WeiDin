import request from './request'
import type {
  Message,
  CreateMessageDto,
  UpdateMessageStatusDto,
  PaginationParams
} from '@/types'

/** 消息 API：所有操作均基于 relationId 定位分表 */
export const messageApi = {
  getById: (relationId: string, id: string): Promise<Message> => {
    return request.get(`/messages/relation/${relationId}/${id}`)
  },

  getByRelationId: (relationId: string, params: PaginationParams): Promise<Message[]> => {
    return request.get(`/messages/relation/${relationId}`, { params })
  },

  send: (data: CreateMessageDto): Promise<Message> => {
    return request.post('/messages', data)
  },

  delete: (relationId: string, id: string): Promise<void> => {
    return request.delete(`/messages/relation/${relationId}/${id}`)
  },

  updateStatus: (relationId: string, id: string, data: UpdateMessageStatusDto): Promise<void> => {
    return request.put(`/messages/relation/${relationId}/${id}/status`, data)
  },

  markAsRead: (relationId: string, id: string): Promise<void> => {
    return request.post(`/messages/relation/${relationId}/${id}/read`)
  },

  markAllAsRead: (relationId: string): Promise<string[]> => {
    return request.post(`/messages/relation/${relationId}/read-all`)
  },

  markAsDelivered: (relationId: string, id: string): Promise<void> => {
    return request.post(`/messages/relation/${relationId}/${id}/delivered`)
  },

  searchByRelation: (relationId: string, params: { keyword: string } & PaginationParams): Promise<Message[]> => {
    return request.get(`/messages/relation/${relationId}/search`, { params })
  },
}
