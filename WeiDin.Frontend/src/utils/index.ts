import dayjs from 'dayjs'
import relativeTime from 'dayjs/plugin/relativeTime'
import 'dayjs/locale/zh-cn'

// 配置dayjs
dayjs.extend(relativeTime)
dayjs.locale('zh-cn')

// 格式化时间
export const formatTime = (time: string | Date) => {
  return dayjs(time).format('YYYY-MM-DD HH:mm:ss')
}

// 格式化相对时间
export const formatRelativeTime = (time: string | Date) => {
  return dayjs(time).fromNow()
}

// 格式化聊天时间
export const formatChatTime = (time: string | Date) => {
  const now = dayjs()
  const target = dayjs(time)
  
  if (now.diff(target, 'day') === 0) {
    return target.format('HH:mm')
  } else if (now.diff(target, 'day') === 1) {
    return '昨天 ' + target.format('HH:mm')
  } else if (now.diff(target, 'day') < 7) {
    return target.format('dddd HH:mm')
  } else {
    return target.format('MM-DD HH:mm')
  }
}

// 防抖函数
export const debounce = <T extends (...args: any[]) => any>(
  func: T,
  wait: number
): ((...args: Parameters<T>) => void) => {
  let timeout: NodeJS.Timeout | null = null
  
  return (...args: Parameters<T>) => {
    if (timeout) clearTimeout(timeout)
    timeout = setTimeout(() => func(...args), wait)
  }
}

// 节流函数
export const throttle = <T extends (...args: any[]) => any>(
  func: T,
  wait: number
): ((...args: Parameters<T>) => void) => {
  let lastTime = 0
  
  return (...args: Parameters<T>) => {
    const now = Date.now()
    if (now - lastTime >= wait) {
      lastTime = now
      func(...args)
    }
  }
}

// 生成随机ID
export const generateId = (): string => {
  return Math.random().toString(36).substr(2, 9)
}

// 文件大小格式化
export const formatFileSize = (bytes: number): string => {
  if (bytes === 0) return '0 B'
  
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

// 获取文件扩展名
export const getFileExtension = (filename: string): string => {
  return filename.slice((filename.lastIndexOf('.') - 1 >>> 0) + 2)
}

// 检查文件类型
export const isImageFile = (filename: string): boolean => {
  const imageExtensions = ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp']
  const ext = getFileExtension(filename).toLowerCase()
  return imageExtensions.includes(ext)
}

export const isVideoFile = (filename: string): boolean => {
  const videoExtensions = ['mp4', 'avi', 'mov', 'wmv', 'flv', 'webm']
  const ext = getFileExtension(filename).toLowerCase()
  return videoExtensions.includes(ext)
}

export const isAudioFile = (filename: string): boolean => {
  const audioExtensions = ['mp3', 'wav', 'ogg', 'aac', 'flac']
  const ext = getFileExtension(filename).toLowerCase()
  return audioExtensions.includes(ext)
}

// 下载文件
export const downloadFile = (url: string, filename: string) => {
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
}

// 复制到剪贴板
export const copyToClipboard = async (text: string): Promise<boolean> => {
  try {
    await navigator.clipboard.writeText(text)
    return true
  } catch (error) {
    // 降级方案
    const textArea = document.createElement('textarea')
    textArea.value = text
    document.body.appendChild(textArea)
    textArea.select()
    const success = document.execCommand('copy')
    document.body.removeChild(textArea)
    return success
  }
}

// 验证邮箱
export const isValidEmail = (email: string): boolean => {
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  return emailRegex.test(email)
}

// 验证手机号
export const isValidPhone = (phone: string): boolean => {
  const phoneRegex = /^1[3-9]\d{9}$/
  return phoneRegex.test(phone)
}

// 验证用户名
export const isValidUsername = (username: string): boolean => {
  const usernameRegex = /^[a-zA-Z0-9_]{3,20}$/
  return usernameRegex.test(username)
}

// 生成头像颜色
export const getAvatarColor = (name: string): string => {
  const colors = [
    '#f56a00', '#7265e6', '#ffbf00', '#00a2ae',
    '#87d068', '#108ee9', '#f50', '#2db7f5',
    '#ff4d4f', '#52c41a', '#fa8c16', '#722ed1'
  ]
  
  let hash = 0
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash)
  }
  
  return colors[Math.abs(hash) % colors.length]
}

// 截断文本
export const truncateText = (text: string, maxLength: number): string => {
  if (text.length <= maxLength) return text
  return text.slice(0, maxLength) + '...'
}

// 深拷贝
export const deepClone = <T>(obj: T): T => {
  if (obj === null || typeof obj !== 'object') return obj
  if (obj instanceof Date) return new Date(obj.getTime()) as unknown as T
  if (obj instanceof Array) return obj.map(item => deepClone(item)) as unknown as T
  if (typeof obj === 'object') {
    const clonedObj = {} as T
    for (const key in obj) {
      if (obj.hasOwnProperty(key)) {
        clonedObj[key] = deepClone(obj[key])
      }
    }
    return clonedObj
  }
  return obj
}
