/**
 * 实时事件总线
 * 提供事件发布订阅功能，与 SignalR 和 Pinia 解耦
 */

type EventHandler = (payload: any) => void

class RealtimeBus {
  private handlers = new Map<string, Set<EventHandler>>()

  /**
   * 注册事件监听器
   * @param event 事件名称
   * @param handler 事件处理器
   */
  on(event: string, handler: EventHandler): void {
    if (!this.handlers.has(event)) {
      this.handlers.set(event, new Set())
    }
    this.handlers.get(event)!.add(handler)
  }

  /**
   * 移除事件监听器
   * @param event 事件名称
   * @param handler 事件处理器（可选，不提供则移除该事件的所有处理器）
   */
  off(event: string, handler?: EventHandler): void {
    const eventHandlers = this.handlers.get(event)
    if (!eventHandlers) return

    if (handler) {
      eventHandlers.delete(handler)
      if (eventHandlers.size === 0) {
        this.handlers.delete(event)
      }
    } else {
      this.handlers.delete(event)
    }
  }

  /**
   * 触发事件
   * @param event 事件名称
   * @param payload 事件数据
   */
  emit(event: string, payload?: any): void {
    const eventHandlers = this.handlers.get(event)
    if (eventHandlers) {
      eventHandlers.forEach(handler => {
        try {
          handler(payload)
        } catch (error) {
          console.error(`事件处理器执行失败 [${event}]:`, error)
        }
      })
    }
  }

  /**
   * 清除所有事件监听器
   */
  clear(): void {
    this.handlers.clear()
  }

  /**
   * 获取指定事件的所有处理器数量
   */
  getHandlerCount(event: string): number {
    return this.handlers.get(event)?.size || 0
  }
}

// 导出单例
export const realtimeBus = new RealtimeBus()
