import { ref } from 'vue'
import { defineStore } from 'pinia'
import {
  getAeroDataBoxStatus,
  getFlight,
  searchFlights as requestFlights,
} from '@/api/flights'
import type {
  AeroDataBoxProviderStatus,
  Airport,
  ApiMeta,
  FlightItinerary,
} from '@/types/api'

export type FlightSearchMode = 'route' | 'number'

function today(): string {
  const now = new Date()
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 10)
}

export const useFlightsStore = defineStore('flights', () => {
  const searchMode = ref<FlightSearchMode>('route')
  const origin = ref<Airport | null>(null)
  const destination = ref<Airport | null>(null)
  const date = ref(today())
  const flightNumber = ref('')
  const itineraries = ref<FlightItinerary[]>([])
  const resultMeta = ref<ApiMeta | null>(null)
  const providerStatus = ref<AeroDataBoxProviderStatus | null>(null)
  const loading = ref(false)
  const searched = ref(false)
  const error = ref<string | null>(null)

  function resetResults(): void {
    itineraries.value = []
    resultMeta.value = null
    searched.value = false
    error.value = null
  }

  function setMode(mode: FlightSearchMode): void {
    searchMode.value = mode
    resetResults()
  }

  function swapAirports(): void {
    const currentOrigin = origin.value
    origin.value = destination.value
    destination.value = currentOrigin
  }

  async function searchRoute(): Promise<void> {
    if (!origin.value || !destination.value || !date.value) {
      error.value = '請選擇出發機場、抵達機場與日期。'
      return
    }

    if (origin.value.iata === destination.value.iata) {
      error.value = '出發與抵達機場不可相同。'
      return
    }

    await run(async () => {
      const response = await requestFlights(
        origin.value!.iata,
        destination.value!.iata,
        date.value,
      )
      itineraries.value = response.data
      resultMeta.value = response.meta
    })
  }

  async function searchNumber(): Promise<void> {
    const normalized = flightNumber.value.replace(/[^a-z0-9]/gi, '').toUpperCase()
    if (normalized.length < 2 || !date.value) {
      error.value = '請輸入有效的航班編號與日期。'
      return
    }

    flightNumber.value = normalized
    await run(async () => {
      const response = await getFlight(normalized, date.value)
      itineraries.value = response.data
      resultMeta.value = response.meta
    })
  }

  async function loadProviderStatus(): Promise<void> {
    try {
      providerStatus.value = (await getAeroDataBoxStatus()).data
    } catch {
      providerStatus.value = null
    }
  }

  async function run(action: () => Promise<void>): Promise<void> {
    loading.value = true
    searched.value = true
    error.value = null
    try {
      await action()
    } catch {
      itineraries.value = []
      resultMeta.value = null
      error.value = '目前無法完成航班查詢，請稍後再試。'
    } finally {
      loading.value = false
      await loadProviderStatus()
    }
  }

  return {
    searchMode,
    origin,
    destination,
    date,
    flightNumber,
    itineraries,
    resultMeta,
    providerStatus,
    loading,
    searched,
    error,
    resetResults,
    setMode,
    swapAirports,
    searchRoute,
    searchNumber,
    loadProviderStatus,
  }
})
