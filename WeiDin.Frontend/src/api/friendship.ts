import request from './request'
import type { 
  Friendship, 
  CreateFriendshipDto, 
  UpdateFriendshipDto, 
  Blacklist, 
  CreateBlacklistDto 
} from '@/types'

// 好友相关API
export const friendshipApi = {
  // 获取好友列表
  getFriendships: (): Promise<Friendship[]> => {
    return request.get('/friendships')
  },

  // 获取好友详情
  getFriendshipById: (id: string): Promise<Friendship> => {
    return request.get(`/friendships/${id}`)
  },

  // 添加好友
  addFriend: (data: CreateFriendshipDto): Promise<Friendship> => {
    return request.post('/friendships', data)
  },

  // 删除好友
  removeFriend: (id: string): Promise<void> => {
    return request.delete(`/friendships/${id}`)
  },

  // 更新好友信息
  updateFriendship: (id: string, data: UpdateFriendshipDto): Promise<Friendship> => {
    return request.put(`/friendships/${id}`, data)
  },

  // 添加到黑名单
  addToBlacklist: (data: CreateBlacklistDto): Promise<Blacklist> => {
    return request.post('/friendships/blacklist', data)
  },

  // 获取黑名单
  getBlacklist: (): Promise<Blacklist[]> => {
    return request.get('/friendships/blacklist')
  },

  // 从黑名单移除
  removeFromBlacklist: (id: string): Promise<void> => {
    return request.delete(`/friendships/blacklist/${id}`)
  },

  // 检查是否为好友
  isFriend: (friendId: string): Promise<boolean> => {
    return request.get(`/friendships/is-friend/${friendId}`)
  },

  // 接受好友申请
  acceptFriendRequest: (id: string): Promise<Friendship> => {
    return request.post(`/friendships/${id}/accept`)
  },

  // 拒绝好友申请
  rejectFriendRequest: (id: string): Promise<void> => {
    return request.post(`/friendships/${id}/reject`)
  },

  // 获取待处理的好友申请（收到的申请）
  getPendingRequests: (): Promise<Friendship[]> => {
    return request.get('/friendships/pending')
  },

  // 获取已发送的好友申请
  getSentRequests: (): Promise<Friendship[]> => {
    return request.get('/friendships/sent')
  },
}
