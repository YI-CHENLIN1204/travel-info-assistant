import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getSystemHealth } from '@/api/system'
import { initializeLiff, type LiffState } from '@/services/liff'

type ApiStatus = 'checking' | 'online' | 'degraded' | 'offline'

export const useAppStore = defineStore('app', () => {
  const apiStatus = ref<ApiStatus>('checking')
  const liffState = ref<LiffState>({ enabled: false, inClient: false, initialized: false })

  async function initialize(): Promise<void> {
    const [healthResult, liffResult] = await Promise.allSettled([
      getSystemHealth(),
      initializeLiff(),
    ])

    if (healthResult.status === 'fulfilled') {
      apiStatus.value = healthResult.value.status === 'healthy' ? 'online' : 'degraded'
    } else {
      apiStatus.value = 'offline'
    }

    if (liffResult.status === 'fulfilled') {
      liffState.value = liffResult.value
    }
  }

  return { apiStatus, liffState, initialize }
})
