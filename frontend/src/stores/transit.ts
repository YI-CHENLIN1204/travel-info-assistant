import { ref } from 'vue'
import { defineStore } from 'pinia'
import {
  getBusArrivals,
  getBusStops,
  getMetroArrivals,
  getTdxStatus,
  searchBusRoutes as requestBusRoutes,
  searchMetroStations as requestMetroStations,
} from '@/api/transit'
import type {
  ApiMeta,
  MetroStation,
  TdxProviderStatus,
  TransitArrival,
  TransitRoute,
  TransitStop,
} from '@/types/api'

export type TransitModeKey = 'bus' | 'metro'

export const useTransitStore = defineStore('transit', () => {
  const activeMode = ref<TransitModeKey>('bus')
  const busQuery = ref('')
  const metroQuery = ref('')
  const routes = ref<TransitRoute[]>([])
  const stations = ref<MetroStation[]>([])
  const selectedRoute = ref<TransitRoute | null>(null)
  const selectedDirection = ref(0)
  const stops = ref<TransitStop[]>([])
  const selectedStop = ref<TransitStop | null>(null)
  const selectedStation = ref<MetroStation | null>(null)
  const arrivals = ref<TransitArrival[]>([])
  const resultMeta = ref<ApiMeta | null>(null)
  const providerStatus = ref<TdxProviderStatus | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  function resetResults(): void {
    routes.value = []
    stations.value = []
    selectedRoute.value = null
    selectedDirection.value = 0
    stops.value = []
    selectedStop.value = null
    selectedStation.value = null
    arrivals.value = []
    resultMeta.value = null
    error.value = null
  }

  async function searchBusRoutes(cityId: string): Promise<void> {
    await run(async () => {
      const response = await requestBusRoutes(cityId, busQuery.value.trim())
      routes.value = response.data
      selectedRoute.value = null
      stops.value = []
      selectedStop.value = null
      arrivals.value = []
      resultMeta.value = response.meta
    })
  }

  async function chooseBusRoute(cityId: string, route: TransitRoute): Promise<void> {
    selectedRoute.value = route
    selectedDirection.value = route.directions[0]?.direction ?? 0
    selectedStop.value = null
    arrivals.value = []
    await loadBusStops(cityId)
  }

  async function chooseBusDirection(cityId: string, direction: number): Promise<void> {
    selectedDirection.value = direction
    selectedStop.value = null
    arrivals.value = []
    await loadBusStops(cityId)
  }

  async function loadBusStops(cityId: string): Promise<void> {
    if (!selectedRoute.value) return

    await run(async () => {
      const response = await getBusStops(
        cityId,
        selectedRoute.value!.nameZh,
        selectedDirection.value,
      )
      stops.value = response.data
      selectedStop.value = null
      arrivals.value = []
      resultMeta.value = response.meta
    })
  }

  async function chooseBusStop(cityId: string, stop: TransitStop): Promise<void> {
    if (!selectedRoute.value) return

    selectedStop.value = stop
    await run(async () => {
      const response = await getBusArrivals(
        cityId,
        selectedRoute.value!.nameZh,
        selectedDirection.value,
        stop.id,
      )
      arrivals.value = response.data
      resultMeta.value = response.meta
    })
  }

  async function refreshBusArrivals(cityId: string): Promise<void> {
    if (selectedStop.value) {
      await chooseBusStop(cityId, selectedStop.value)
    }
  }

  async function searchMetroStations(cityId: string): Promise<void> {
    await run(async () => {
      const response = await requestMetroStations(cityId, metroQuery.value.trim())
      stations.value = response.data
      selectedStation.value = null
      arrivals.value = []
      resultMeta.value = response.meta
    })
  }

  async function chooseMetroStation(cityId: string, station: MetroStation): Promise<void> {
    selectedStation.value = station
    await run(async () => {
      const response = await getMetroArrivals(cityId, station.id)
      arrivals.value = response.data
      resultMeta.value = response.meta
    })
  }

  async function refreshMetroArrivals(cityId: string): Promise<void> {
    if (selectedStation.value) {
      await chooseMetroStation(cityId, selectedStation.value)
    }
  }

  async function loadProviderStatus(): Promise<void> {
    try {
      providerStatus.value = (await getTdxStatus()).data
    } catch {
      providerStatus.value = null
    }
  }

  async function run(action: () => Promise<void>): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await action()
    } catch {
      error.value = '目前無法完成查詢，請稍後再試。'
    } finally {
      loading.value = false
      await loadProviderStatus()
    }
  }

  return {
    activeMode,
    busQuery,
    metroQuery,
    routes,
    stations,
    selectedRoute,
    selectedDirection,
    stops,
    selectedStop,
    selectedStation,
    arrivals,
    resultMeta,
    providerStatus,
    loading,
    error,
    resetResults,
    searchBusRoutes,
    chooseBusRoute,
    chooseBusDirection,
    chooseBusStop,
    refreshBusArrivals,
    searchMetroStations,
    chooseMetroStation,
    refreshMetroArrivals,
    loadProviderStatus,
  }
})
