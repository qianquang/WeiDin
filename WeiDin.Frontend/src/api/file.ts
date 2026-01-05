import request from './request'
import type { FileUploadResult } from '@/types'

// 文件相关API
export const fileApi = {
  // 上传单个文件
  uploadFile: (file: File): Promise<FileUploadResult> => {
    const formData = new FormData()
    formData.append('file', file)
    
    return request.post('/files/upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    })
  },

  // 上传多个文件
  uploadFiles: (files: File[]): Promise<FileUploadResult[]> => {
    const formData = new FormData()
    files.forEach(file => {
      formData.append('files', file)
    })
    
    return request.post('/files/upload-multiple', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    })
  },

  // 获取文件
  getFile: (fileName: string): Promise<Blob> => {
    return request.get(`/files/${fileName}`, {
      responseType: 'blob',
    })
  },

  // 删除文件
  deleteFile: (fileName: string): Promise<void> => {
    return request.delete(`/files/${fileName}`)
  },

  // 下载文件
  downloadFile: async (fileName: string, downloadName?: string): Promise<void> => {
    try {
      const blob = await fileApi.getFile(fileName)
      const url = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = downloadName || fileName
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      window.URL.revokeObjectURL(url)
    } catch (error) {
      console.error('下载文件失败:', error)
      throw error
    }
  },
}
