/**
 * 失效标记 Store
 * 记录哪些域的数据可能已过期，页面进入时根据失效标记决定是否 refetch
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useInvalidationStore = defineStore('invalidation', () => {
  // 失效标记：Record<domain, boolean>
  const invalidations = ref<Record<string, boolean>>({})

  /**
   * 标记某域数据失效
   * @param domain 域名称，如 'friends', 'chat', 'groups'
   */
  const markStale = (domain: string): void => {
    invalidations.value[domain] = true
  }

  /**
   * 清除某域的失效标记
   * @param domain 域名称
   */
  const clear = (domain: string): void => {
    delete invalidations.value[domain]
  }

  /**
   * 检查某域是否失效
   * @param domain 域名称
   * @returns 是否失效
   */
  const isStale = (domain: string): boolean => {
    return invalidations.value[domain] === true
  }

  /**
   * 清除所有失效标记
   */
  const clearAll = (): void => {
    invalidations.value = {}
  }

  return {
    invalidations,
    markStale,
    clear,
    isStale,
    clearAll,
  }
})
