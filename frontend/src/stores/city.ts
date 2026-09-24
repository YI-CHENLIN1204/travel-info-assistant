import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { getCities, resolveCity } from '@/api/system'
import type { City } from '@/types/api'

const cityStorageKey = 'lastSelectedCityId'

const fallbackTaipei: City = {
  id: 'ffb8f976-2fd5-46c7-b543-ebdcd3283973',
  code: 'taipei',
  nameZh: '台北',
  nameEn: 'Taipei',
  countryCode: 'TW',
  timeZone: 'Asia/Taipei',
  centerLatitude: 25.0375,
  centerLongitude: 121.5637,
  services: [],
}

type LocationStatus = 'idle' | 'requesting' | 'resolved' | 'denied' | 'unavailable' | 'unsupported'
export interface CurrentCoordinates {
  latitude: number
  longitude: number
}

export const useCityStore = defineStore('city', () => {
  const cities = ref<City[]>([])
  const selectedCityId = ref<string | null>(null)
  const suggestedCity = ref<City | null>(null)
  const currentCoordinates = ref<CurrentCoordinates | null>(null)
  const locationStatus = ref<LocationStatus>('idle')
  const loading = ref(false)

  const currentCity = computed(
    () => cities.value.find((city) => city.id === selectedCityId.value) ?? fallbackTaipei,
  )

  async function initialize(): Promise<void> {
    loading.value = true
    try {
      cities.value = await getCities()
    } catch {
      cities.value = [fallbackTaipei]
    } finally {
      const previousCityId = window.localStorage.getItem(cityStorageKey)
      const previousStillAvailable = cities.value.some((city) => city.id === previousCityId)
      const taipei = cities.value.find((city) => city.code === 'taipei') ?? cities.value[0]
      selectedCityId.value = previousStillAvailable ? previousCityId : (taipei?.id ?? fallbackTaipei.id)
      loading.value = false
    }
  }

  function selectCity(cityId: string): void {
    if (!cities.value.some((city) => city.id === cityId)) return
    selectedCityId.value = cityId
    window.localStorage.setItem(cityStorageKey, cityId)
  }

  async function requestLocation(): Promise<void> {
    if (!('geolocation' in navigator) || cities.value.length === 0) {
      locationStatus.value = 'unavailable'
      return
    }

    locationStatus.value = 'requesting'
    try {
      const position = await getBrowserPosition()
      currentCoordinates.value = {
        latitude: position.coords.latitude,
        longitude: position.coords.longitude,
      }
      const result = await resolveCity(position.coords.latitude, position.coords.longitude)

      if (!result.supported || !result.city) {
        locationStatus.value = 'unsupported'
        return
      }

      locationStatus.value = 'resolved'
      if (result.city.id !== selectedCityId.value) {
        suggestedCity.value = result.city
      }
    } catch (error) {
      locationStatus.value = isPermissionDenied(error) ? 'denied' : 'unavailable'
    }
  }

  function acceptSuggestedCity(): void {
    if (!suggestedCity.value) return
    selectCity(suggestedCity.value.id)
    suggestedCity.value = null
  }

  function dismissSuggestedCity(): void {
    suggestedCity.value = null
  }

  return {
    cities,
    currentCity,
    selectedCityId,
    suggestedCity,
    currentCoordinates,
    locationStatus,
    loading,
    initialize,
    selectCity,
    requestLocation,
    acceptSuggestedCity,
    dismissSuggestedCity,
  }
})

function getBrowserPosition(): Promise<GeolocationPosition> {
  return new Promise((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(resolve, reject, {
      enableHighAccuracy: false,
      timeout: 8_000,
      maximumAge: 5 * 60_000,
    })
  })
}

function isPermissionDenied(error: unknown): boolean {
  return typeof error === 'object' && error !== null && 'code' in error && error.code === 1
}
