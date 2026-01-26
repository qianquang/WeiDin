import axios, { type AxiosInstance, type AxiosRequestConfig, type AxiosResponse } from 'axios'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/auth'

// 创建axios实例
const request: AxiosInstance = axios.create({
  baseURL: '/api/v1',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
})

// 请求拦截器
request.interceptors.request.use(
  (config: any) => {
    const authStore = useAuthStore()
    const token = authStore.token
    
    if (token) {
      config.headers = {
        ...config.headers,
        Authorization: `Bearer ${token}`,
      }
    }
    
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// 响应拦截器
request.interceptors.response.use(
  (response: AxiosResponse) => {
    const { data } = response
    
    // 如果后端返回的是标准格式
    if (data && typeof data === 'object' && 'code' in data) {
      if (data.code === 200 || data.code === 0) {
        return data.data
      } else {
        ElMessage.error(data.message || '请求失败')
        return Promise.reject(new Error(data.message || '请求失败'))
      }
    }
    
    return data
  },
  (error) => {
    if (error.response) {
      const { status, data } = error.response
      
      // 提取错误消息（支持多种格式）
      let errorMessage = '请求失败'
      if (data) {
        if (typeof data === 'string') {
          errorMessage = data
        } else if (data.message) {
          errorMessage = data.message
        } else if (data.error?.message) {
          errorMessage = data.error.message
        } else if (data.title) {
          errorMessage = data.title
        }
      }
      
      switch (status) {
        case 400:
          // Bad Request - 业务逻辑错误（如用户名已存在）
          ElMessage.error(errorMessage)
          break
        case 401:
          // Unauthorized - 显示后端返回的具体错误消息（如"用户名或密码错误"）
          // 如果后端没有返回具体消息，则显示默认消息
          const finalMessage = errorMessage !== '请求失败' ? errorMessage : '未授权，请重新登录'
          ElMessage.error(finalMessage)
          
          // 只有在已登录的情况下才执行退出登录（避免登录页面触发logout）
          const authStore = useAuthStore()
          if (authStore.isLoggedIn) {
            authStore.logout()
          }
          break
        case 403:
          ElMessage.error('拒绝访问')
          break
        case 404:
          ElMessage.error('请求的资源不存在')
          break
        case 500:
          ElMessage.error(errorMessage || '服务器内部错误')
          break
        default:
          ElMessage.error(errorMessage || `请求失败 (${status})`)
      }
    } else if (error.request) {
      ElMessage.error('网络错误，请检查网络连接')
    } else {
      ElMessage.error('请求配置错误')
    }
    
    return Promise.reject(error)
  }
)

export default request
