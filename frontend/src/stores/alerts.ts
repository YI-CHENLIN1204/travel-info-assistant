import { ref } from 'vue'
import { defineStore } from 'pinia'
import { getCityAlerts, getPopularAlerts } from '@/api/alerts'
import type { ApiMeta, TravelAlert } from '@/types/api'

export const useAlertStore = defineStore('alerts', () => {
  const cityAlerts = ref<TravelAlert[]>([])
  const popularAlerts = ref<TravelAlert[]>([])
  const cityMeta = ref<ApiMeta | null>(null)
  const popularMeta = ref<ApiMeta | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  let requestSequence = 0

  async function load(cityId: string): Promise<void> {
    const sequence = ++requestSequence
    loading.value = true
    error.value = null
    try {
      const [city, popular] = await Promise.all([
        getCityAlerts(cityId),
        getPopularAlerts(),
      ])
      if (sequence !== requestSequence) return

      cityAlerts.value = city.data
      popularAlerts.value = popular.data
      cityMeta.value = city.meta
      popularMeta.value = popular.meta
      if (popular.meta.dataStatus === 'unavailable') {
        error.value = popular.meta.message ?? '官方旅遊警示目前無法更新。'
      }
    } catch {
      if (sequence !== requestSequence) return
      cityAlerts.value = []
      popularAlerts.value = []
      cityMeta.value = null
      popularMeta.value = null
      error.value = '目前無法完成旅遊警示查詢，請稍後再試。'
    } finally {
      if (sequence === requestSequence) loading.value = false
    }
  }

  return {
    cityAlerts,
    popularAlerts,
    cityMeta,
    popularMeta,
    loading,
    error,
    load,
  }
})
