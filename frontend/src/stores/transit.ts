import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  getBusArrivals,
  getBusStops,
  getMetroArrivals,
  getMetroStatus,
  getRailArrivals,
  getTdxStatus,
  searchBusRoutes as requestBusRoutes,
  searchMetroRoutes as requestMetroRoutes,
  searchMetroStations as requestMetroStations,
  searchRailStations as requestRailStations,
} from '@/api/transit'
import type {
  ApiMeta,
  MetroServiceStatus,
  MetroStation,
  RailStation,
  TdxProviderStatus,
  TransitArrival,
  TransitRoute,
  TransitStop,
} from '@/types/api'

export type TransitModeKey = 'bus' | 'metro' | 'rail'

export const tokyoMetroStatusRefreshMilliseconds = 60_000

export function filterTokyoMetroStations(
  stations: MetroStation[],
  route: TransitRoute | null,
): MetroStation[] {
  if (!route) return stations

  const stationOrder = new Map(
    route.stationNames.map((stationName, index) => [stationName, index]),
  )

  return stations
    .filter((station) => station.railwayId === route.id)
    .map((station, index) => ({ station, index }))
    .sort((left, right) => {
      const leftOrder = stationOrder.get(left.station.nameZh)
      const rightOrder = stationOrder.get(right.station.nameZh)

      if (leftOrder === undefined && rightOrder === undefined) {
        return left.index - right.index
      }
      if (leftOrder === undefined) return 1
      if (rightOrder === undefined) return -1
      return leftOrder - rightOrder
    })
    .map(({ station }) => station)
}

export function findTokyoMetroStatus(
  statuses: MetroServiceStatus[],
  route: TransitRoute | null,
): MetroServiceStatus | null {
  if (!route) return null
  return statuses.find((status) => status.lineId === route.id) ?? null
}

export function isTokyoMetroStatusCurrent(
  status: MetroServiceStatus,
  now: Date,
): boolean {
  if (!status.validUntil) return false

  const validUntil = Date.parse(status.validUntil)
  return Number.isFinite(validUntil) && validUntil > now.getTime()
}

export const useTransitStore = defineStore('transit', () => {
  const activeMode = ref<TransitModeKey>('bus')
  const busQuery = ref('')
  const metroQuery = ref('')
  const railQuery = ref('')
  const routes = ref<TransitRoute[]>([])
  const metroRoutes = ref<TransitRoute[]>([])
  const stations = ref<MetroStation[]>([])
  const metroStatuses = ref<MetroServiceStatus[]>([])
  const metroStatusMeta = ref<ApiMeta | null>(null)
  const railStations = ref<RailStation[]>([])
  const selectedTokyoRoute = ref<TransitRoute | null>(null)
  const selectedTokyoRouteStations = ref<MetroStation[]>([])
  const filteredTokyoStations = computed(() =>
    filterTokyoMetroStations(
      selectedTokyoRoute.value ? selectedTokyoRouteStations.value : stations.value,
      selectedTokyoRoute.value,
    ),
  )
  const selectedTokyoMetroStatus = computed(() =>
    findTokyoMetroStatus(metroStatuses.value, selectedTokyoRoute.value),
  )
  const selectedRoute = ref<TransitRoute | null>(null)
  const selectedDirection = ref(0)
  const stops = ref<TransitStop[]>([])
  const selectedStop = ref<TransitStop | null>(null)
  const selectedStation = ref<MetroStation | null>(null)
  const selectedRailStation = ref<RailStation | null>(null)
  const arrivals = ref<TransitArrival[]>([])
  const resultMeta = ref<ApiMeta | null>(null)
  const providerStatus = ref<TdxProviderStatus | null>(null)
  const loading = ref(false)
  const metroStatusLoading = ref(false)
  const error = ref<string | null>(null)
  let metroStatusRefreshTimer: number | undefined

  function resetResults(): void {
    stopTokyoMetroStatusRefresh()
    routes.value = []
    metroRoutes.value = []
    stations.value = []
    metroStatuses.value = []
    metroStatusMeta.value = null
    railStations.value = []
    selectedTokyoRoute.value = null
    selectedTokyoRouteStations.value = []
    selectedRoute.value = null
    selectedDirection.value = 0
    stops.value = []
    selectedStop.value = null
    selectedStation.value = null
    selectedRailStation.value = null
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

  async function searchTokyoMetro(cityId: string): Promise<void> {
    await run(async () => {
      const query = metroQuery.value.trim()
      const [routeResponse, stationResponse, statusResponse] = await Promise.all([
        requestMetroRoutes(cityId, query),
        requestMetroStations(cityId, query),
        getMetroStatus(cityId),
      ])
      metroRoutes.value = routeResponse.data
      stations.value = stationResponse.data
      metroStatuses.value = statusResponse.data
      metroStatusMeta.value = statusResponse.meta
      selectedTokyoRoute.value = null
      selectedTokyoRouteStations.value = []
      selectedStation.value = null
      arrivals.value = []
      resultMeta.value =
        routeResponse.meta.dataStatus === 'unavailable'
          ? routeResponse.meta
          : stationResponse.meta
    })
  }

  async function refreshTokyoMetroStatus(cityId: string): Promise<void> {
    if (metroStatusLoading.value) return

    metroStatusLoading.value = true
    try {
      const response = await getMetroStatus(cityId)
      metroStatuses.value = response.data
      metroStatusMeta.value = response.meta
    } catch {
      const previousMeta = metroStatusMeta.value
      metroStatusMeta.value = {
        dataStatus: 'unavailable',
        source: previousMeta?.source ?? 'ODPT',
        sourceUpdatedAt: previousMeta?.sourceUpdatedAt ?? null,
        fetchedAt: new Date().toISOString(),
        stale: true,
        message: '目前無法更新官方運行狀態，請稍後再試。',
      }
    } finally {
      metroStatusLoading.value = false
    }
  }

  function startTokyoMetroStatusRefresh(cityId: string): void {
    stopTokyoMetroStatusRefresh()
    void refreshTokyoMetroStatus(cityId)
    metroStatusRefreshTimer = window.setInterval(() => {
      void refreshTokyoMetroStatus(cityId)
    }, tokyoMetroStatusRefreshMilliseconds)
  }

  function stopTokyoMetroStatusRefresh(): void {
    if (metroStatusRefreshTimer === undefined) return

    window.clearInterval(metroStatusRefreshTimer)
    metroStatusRefreshTimer = undefined
  }

  async function chooseTokyoRoute(cityId: string, route: TransitRoute): Promise<void> {
    const nextRoute = selectedTokyoRoute.value?.id === route.id ? null : route
    selectedTokyoRoute.value = nextRoute
    selectedTokyoRouteStations.value = []

    if (
      selectedStation.value &&
      nextRoute &&
      selectedStation.value.railwayId !== nextRoute.id
    ) {
      selectedStation.value = null
      arrivals.value = []
    }

    if (!nextRoute) return

    await run(async () => {
      const response = await requestMetroStations(cityId, nextRoute.id)
      if (selectedTokyoRoute.value?.id !== nextRoute.id) return

      selectedTokyoRouteStations.value = response.data
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

  async function searchRailStations(cityId: string): Promise<void> {
    await run(async () => {
      const response = await requestRailStations(cityId, railQuery.value.trim())
      railStations.value = response.data
      selectedRailStation.value = null
      arrivals.value = []
      resultMeta.value = response.meta
    })
  }

  async function chooseRailStation(cityId: string, station: RailStation): Promise<void> {
    selectedRailStation.value = station
    await run(async () => {
      const response = await getRailArrivals(cityId, station.id)
      arrivals.value = response.data
      resultMeta.value = response.meta
    })
  }

  async function refreshRailArrivals(cityId: string): Promise<void> {
    if (selectedRailStation.value) {
      await chooseRailStation(cityId, selectedRailStation.value)
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
    railQuery,
    routes,
    metroRoutes,
    stations,
    metroStatuses,
    metroStatusMeta,
    railStations,
    selectedTokyoRoute,
    filteredTokyoStations,
    selectedTokyoMetroStatus,
    selectedRoute,
    selectedDirection,
    stops,
    selectedStop,
    selectedStation,
    selectedRailStation,
    arrivals,
    resultMeta,
    providerStatus,
    loading,
    metroStatusLoading,
    error,
    resetResults,
    searchBusRoutes,
    chooseBusRoute,
    chooseBusDirection,
    chooseBusStop,
    refreshBusArrivals,
    searchMetroStations,
    searchTokyoMetro,
    refreshTokyoMetroStatus,
    startTokyoMetroStatusRefresh,
    stopTokyoMetroStatusRefresh,
    chooseTokyoRoute,
    chooseMetroStation,
    refreshMetroArrivals,
    searchRailStations,
    chooseRailStation,
    refreshRailArrivals,
    loadProviderStatus,
  }
})
