import { ref } from 'vue'
import { defineStore } from 'pinia'
import { getCityWeather, getLocationWeather } from '@/api/weather'
import type { ApiMeta, ApiResponse, WeatherForecast } from '@/types/api'

export type WeatherScope = 'city' | 'location'

export const useWeatherStore = defineStore('weather', () => {
  const forecast = ref<WeatherForecast | null>(null)
  const resultMeta = ref<ApiMeta | null>(null)
  const scope = ref<WeatherScope>('city')
  const loading = ref(false)
  const error = ref<string | null>(null)
  let requestSequence = 0

  async function loadCity(cityId: string): Promise<void> {
    scope.value = 'city'
    await run(() => getCityWeather(cityId))
  }

  async function loadLocation(latitude: number, longitude: number): Promise<void> {
    scope.value = 'location'
    await run(() => getLocationWeather(latitude, longitude))
  }

  async function run(
    request: () => Promise<ApiResponse<WeatherForecast | null>>,
  ): Promise<void> {
    const sequence = ++requestSequence
    loading.value = true
    error.value = null
    try {
      const response = await request()
      if (sequence !== requestSequence) return

      forecast.value = response.data
      resultMeta.value = response.meta
      if (!response.data) {
        error.value = response.meta.message ?? '目前無法取得天氣資料，請稍後再試。'
      }
    } catch {
      if (sequence !== requestSequence) return
      forecast.value = null
      resultMeta.value = null
      error.value = '目前無法完成天氣查詢，請稍後再試。'
    } finally {
      if (sequence === requestSequence) {
        loading.value = false
      }
    }
  }

  return {
    forecast,
    resultMeta,
    scope,
    loading,
    error,
    loadCity,
    loadLocation,
  }
})
