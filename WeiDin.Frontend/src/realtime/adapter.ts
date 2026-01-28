/**
 * SignalR 适配器
 * 负责将 SignalR Hub 事件转发到事件总线
 */

import * as signalR from '@microsoft/signalr'
import { realtimeBus } from './bus'

// Hub 事件名称列表（根据 ChatHub.cs 中定义的事件）
const HUB_EVENTS = [
  'PendingFriendRequestsLoaded',
  'FriendRequestReceived',
  'FriendRequestAccepted',
  'FriendRequestRejected',
  'NewFriendAdded',
  'ReceiveMessage',
  'ReceiveGroupMessage',
  'UserStatusChanged',
  'MessageRead',
  'MessageDelivered',
] as const

type HubEvent = typeof HUB_EVENTS[number]

class SignalRAdapter {
  private attachedConnection: signalR.HubConnection | null = null
  private eventHandlers = new Map<HubEvent, (data: any) => void>()

  /**
   * 将 SignalR 连接附加到适配器
   * 所有 Hub 事件将被转发到事件总线
   */
  attach(conn: signalR.HubConnection): void {
    if (this.attachedConnection === conn) {
      console.warn('SignalR 连接已附加，跳过重复附加')
      return
    }

    // 如果已有连接，先分离
    if (this.attachedConnection) {
      this.detach()
    }

    this.attachedConnection = conn

    // 为每个 Hub 事件注册转发器
    HUB_EVENTS.forEach(eventName => {
      const handler = (data: any) => {
        realtimeBus.emit(eventName, data)
      }
      
      conn.on(eventName, handler)
      this.eventHandlers.set(eventName, handler)
    })

    console.log('SignalR 适配器已附加，已注册', HUB_EVENTS.length, '个事件')
  }

  /**
   * 从适配器分离 SignalR 连接
   * 清理所有事件监听器
   */
  detach(): void {
    if (!this.attachedConnection) {
      return
    }

    // 移除所有事件监听器
    this.eventHandlers.forEach((handler, eventName) => {
      this.attachedConnection!.off(eventName, handler)
    })

    this.eventHandlers.clear()
    this.attachedConnection = null

    console.log('SignalR 适配器已分离')
  }

  /**
   * 检查是否有附加的连接
   */
  isAttached(): boolean {
    return this.attachedConnection !== null
  }

  /**
   * 获取当前附加的连接
   */
  getConnection(): signalR.HubConnection | null {
    return this.attachedConnection
  }
}

// 导出单例
export const signalRAdapter = new SignalRAdapter()
