import { ref } from 'vue'
import { defineStore } from 'pinia'
import { getCityAlerts, getLocalAlerts, getPopularAlerts } from '@/api/alerts'
import type { ApiMeta, LocalAlert, TravelAlert } from '@/types/api'

export const useAlertStore = defineStore('alerts', () => {
  const localAlerts = ref<LocalAlert[]>([])
  const cityAlerts = ref<TravelAlert[]>([])
  const popularAlerts = ref<TravelAlert[]>([])
  const localMeta = ref<ApiMeta | null>(null)
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
      const [local, city, popular] = await Promise.all([
        getLocalAlerts(cityId),
        getCityAlerts(cityId),
        getPopularAlerts(),
      ])
      if (sequence !== requestSequence) return

      localAlerts.value = local.data
      cityAlerts.value = city.data
      popularAlerts.value = popular.data
      localMeta.value = local.meta
      cityMeta.value = city.meta
      popularMeta.value = popular.meta
      if (popular.meta.dataStatus === 'unavailable') {
        error.value = popular.meta.message ?? '官方旅遊警示目前無法更新。'
      }
    } catch {
      if (sequence !== requestSequence) return
      localAlerts.value = []
      cityAlerts.value = []
      popularAlerts.value = []
      localMeta.value = null
      cityMeta.value = null
      popularMeta.value = null
      error.value = '目前無法完成旅遊警示查詢，請稍後再試。'
    } finally {
      if (sequence === requestSequence) loading.value = false
    }
  }

  return {
    localAlerts,
    cityAlerts,
    popularAlerts,
    localMeta,
    cityMeta,
    popularMeta,
    loading,
    error,
    load,
  }
})
