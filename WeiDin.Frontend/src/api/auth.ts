import request from './request'
import type { LoginDto, CreateUserDto, AuthResponse, User, UpdateUserDto, ChangePasswordDto } from '@/types'

// 认证相关API
export const authApi = {
  // 用户注册
  register: (data: CreateUserDto): Promise<AuthResponse> => {
    return request.post('/auth/register', data)
  },

  // 用户登录
  login: (data: LoginDto): Promise<AuthResponse> => {
    return request.post('/auth/login', data)
  },

  // 用户退出
  logout: (userId: string): Promise<void> => {
    return request.post('/auth/logout', { userId })
  },
}

// 用户相关API
export const userApi = {
  // 获取所有用户
  getAllUsers: (): Promise<User[]> => {
    return request.get('/users')
  },

  // 获取用户详情
  getUserById: (id: string): Promise<User> => {
    return request.get(`/users/${id}`)
  },

  // 创建用户
  createUser: (data: CreateUserDto): Promise<User> => {
    return request.post('/users', data)
  },

  // 更新用户信息
  updateUser: (id: string, data: UpdateUserDto): Promise<User> => {
    return request.put(`/users/${id}`, data)
  },

  // 删除用户
  deleteUser: (id: string): Promise<void> => {
    return request.delete(`/users/${id}`)
  },

  // 修改密码
  changePassword: (id: string, data: ChangePasswordDto): Promise<void> => {
    return request.post(`/users/${id}/change-password`, data)
  },

  // 设置在线状态
  setOnlineStatus: (id: string, isOnline: boolean): Promise<void> => {
    return request.post(`/users/${id}/online-status`, isOnline)
  },
}
