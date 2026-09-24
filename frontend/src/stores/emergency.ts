import { ref } from 'vue'
import { defineStore } from 'pinia'
import { getEmergencyInfo } from '@/api/emergency'
import type { ApiMeta, EmergencyInfo } from '@/types/api'

export const useEmergencyStore = defineStore('emergency', () => {
  const info = ref<EmergencyInfo | null>(null)
  const resultMeta = ref<ApiMeta | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  let requestSequence = 0

  async function load(cityId: string): Promise<void> {
    const sequence = ++requestSequence
    loading.value = true
    error.value = null

    try {
      const response = await getEmergencyInfo(cityId)
      if (sequence !== requestSequence) return

      info.value = response.data
      resultMeta.value = response.meta
    } catch {
      if (sequence !== requestSequence) return
      info.value = null
      resultMeta.value = null
      error.value = '目前無法取得應急資訊，請稍後再試。'
    } finally {
      if (sequence === requestSequence) loading.value = false
    }
  }

  return { info, resultMeta, loading, error, load }
})
