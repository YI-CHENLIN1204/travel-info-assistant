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
  RailHeading,
  RailStation,
  TdxProviderStatus,
  TransitArrival,
  TransitDirection,
  TransitRoute,
  TransitStop,
} from '@/types/api'

export type TransitModeKey = 'bus' | 'metro' | 'rail'

export const metroStatusRefreshMilliseconds = 30_000
export const busArrivalRefreshMilliseconds = 15_000
export const metroArrivalRefreshMilliseconds = 15_000
export const railArrivalRefreshMilliseconds = 15_000

export function buildArrivalDirectionOptions(
  arrivals: TransitArrival[],
): TransitDirection[] {
  const destinations = new Map<number, string[]>()
  for (const arrival of arrivals) {
    if (arrival.direction === null) continue

    const names = destinations.get(arrival.direction) ?? []
    const destination = arrival.destinationName?.trim()
    if (destination && !names.includes(destination)) names.push(destination)
    destinations.set(arrival.direction, names)
  }

  return Array.from(destinations.entries()).map(([direction, names]) => {
    const destinationName = names.length ? names.join('／') : null
    return {
      direction,
      headsign: destinationName,
      originName: null,
      destinationName,
    }
  })
}

export function buildFixedDirectionOptions(
  arrivals: TransitArrival[],
): TransitDirection[] {
  return Array.from(
    new Set(
      arrivals
        .map((arrival) => arrival.direction)
        .filter((direction): direction is number => direction !== null),
    ),
  )
    .sort((left, right) => left - right)
    .map((direction) => ({
      direction,
      headsign: null,
      originName: null,
      destinationName: null,
    }))
}

export function filterMetroRouteStations(
  stations: MetroStation[],
  route: TransitRoute | null,
  direction?: number | null,
): MetroStation[] {
  if (!route) return stations

  const stationOrder = new Map(
    route.stationNames.map((stationName, index) => [stationName, index]),
  )

  const ordered = stations
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

  const selectedDirection = route.directions.find((item) => item.direction === direction)
  if (
    selectedDirection?.destinationName &&
    route.stationNames.length > 1 &&
    selectedDirection.destinationName === route.stationNames[0]
  ) {
    return ordered.reverse()
  }
  return ordered
}

export function findMetroRouteStatus(
  statuses: MetroServiceStatus[],
  route: TransitRoute | null,
): MetroServiceStatus | null {
  if (!route) return null
  return statuses.find((status) => status.lineId === route.id) ?? null
}

export function isMetroStatusCurrent(
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
  const selectedMetroRoute = ref<TransitRoute | null>(null)
  const selectedMetroDirection = ref<number | null>(null)
  const selectedMetroRouteStations = ref<MetroStation[]>([])
  const filteredMetroStations = computed(() =>
    filterMetroRouteStations(
      selectedMetroRoute.value ? selectedMetroRouteStations.value : stations.value,
      selectedMetroRoute.value,
      selectedMetroDirection.value,
    ),
  )
  const selectedMetroStatus = computed(() =>
    findMetroRouteStatus(metroStatuses.value, selectedMetroRoute.value),
  )
  const selectedRoute = ref<TransitRoute | null>(null)
  const selectedDirection = ref(0)
  const stops = ref<TransitStop[]>([])
  const selectedStop = ref<TransitStop | null>(null)
  const selectedStation = ref<MetroStation | null>(null)
  const selectedRailStation = ref<RailStation | null>(null)
  const selectedRailHeading = ref<RailHeading | null>(null)
  const arrivals = ref<TransitArrival[]>([])
  const metroDirectionOptions = computed<TransitDirection[]>(() => {
    if (selectedMetroRoute.value?.directions.length) {
      return selectedMetroRoute.value.directions
    }

    return buildArrivalDirectionOptions(arrivals.value)
  })
  const visibleMetroArrivals = computed(() => {
    if (selectedMetroDirection.value === null) return arrivals.value
    const directional = arrivals.value.filter((arrival) => arrival.direction !== null)
    if (directional.length === 0) return arrivals.value
    return arrivals.value.filter(
      (arrival) => arrival.direction === selectedMetroDirection.value,
    )
  })
  const railHeadingOptions: readonly RailHeading[] = ['north', 'south']
  const visibleRailArrivals = computed(() => {
    if (selectedRailHeading.value === null) return arrivals.value
    const directional = arrivals.value.filter((arrival) => arrival.heading !== null)
    if (directional.length === 0) return arrivals.value
    return arrivals.value.filter((arrival) => arrival.heading === selectedRailHeading.value)
  })
  const resultMeta = ref<ApiMeta | null>(null)
  const providerStatus = ref<TdxProviderStatus | null>(null)
  const loading = ref(false)
  const metroStatusLoading = ref(false)
  const error = ref<string | null>(null)
  let busArrivalRefreshTimer: number | undefined
  let metroStatusRefreshTimer: number | undefined
  let metroArrivalRefreshTimer: number | undefined
  let railArrivalRefreshTimer: number | undefined

  function resetResults(): void {
    stopMetroStatusRefresh()
    stopBusArrivalRefresh()
    stopMetroArrivalRefresh()
    stopRailArrivalRefresh()
    routes.value = []
    metroRoutes.value = []
    stations.value = []
    metroStatuses.value = []
    metroStatusMeta.value = null
    railStations.value = []
    selectedMetroRoute.value = null
    selectedMetroDirection.value = null
    selectedMetroRouteStations.value = []
    selectedRoute.value = null
    selectedDirection.value = 0
    stops.value = []
    selectedStop.value = null
    selectedStation.value = null
    selectedRailStation.value = null
    selectedRailHeading.value = null
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
    if (!loading.value && selectedStop.value) {
      await chooseBusStop(cityId, selectedStop.value)
    }
  }

  function startBusArrivalRefresh(cityId: string): void {
    stopBusArrivalRefresh()
    busArrivalRefreshTimer = window.setInterval(() => {
      void refreshBusArrivals(cityId)
    }, busArrivalRefreshMilliseconds)
  }

  function stopBusArrivalRefresh(): void {
    if (busArrivalRefreshTimer === undefined) return

    window.clearInterval(busArrivalRefreshTimer)
    busArrivalRefreshTimer = undefined
  }

  async function searchMetroStations(cityId: string): Promise<void> {
    await run(async () => {
      const response = await requestMetroStations(cityId, metroQuery.value.trim())
      stations.value = response.data
      selectedStation.value = null
      selectedMetroDirection.value = null
      arrivals.value = []
      resultMeta.value = response.meta
    })
  }

  async function searchRouteMetro(cityId: string): Promise<void> {
    await run(async () => {
      const query = metroQuery.value.trim()
      const [routeResponse, stationResponse] = await Promise.all([
        requestMetroRoutes(cityId, query),
        requestMetroStations(cityId, query),
      ])
      metroRoutes.value = routeResponse.data
      stations.value = stationResponse.data
      metroStatuses.value = []
      metroStatusMeta.value = null
      selectedMetroRoute.value = null
      selectedMetroDirection.value = null
      selectedMetroRouteStations.value = []
      selectedStation.value = null
      arrivals.value = []
      resultMeta.value =
        routeResponse.meta.dataStatus === 'unavailable'
          ? routeResponse.meta
          : stationResponse.meta
    })
  }

  async function refreshMetroStatus(cityId: string, routeId?: string): Promise<void> {
    if (metroStatusLoading.value) return

    metroStatusLoading.value = true
    try {
      const response = await getMetroStatus(cityId, routeId)
      metroStatuses.value = response.data
      metroStatusMeta.value = response.meta
    } catch {
      const previousMeta = metroStatusMeta.value
      metroStatusMeta.value = {
        dataStatus: 'unavailable',
        source:
          previousMeta?.source ??
          (routeId?.startsWith('TDX:TRTC:')
            ? 'TDX'
            : routeId?.startsWith('MTR:')
            ? '香港港鐵開放數據'
            : routeId?.startsWith('LTA:')
              ? '新加坡 LTA DataMall'
              : 'ODPT'),
        sourceUpdatedAt: previousMeta?.sourceUpdatedAt ?? null,
        fetchedAt: new Date().toISOString(),
        stale: true,
        message: '目前無法更新官方運行狀態，請稍後再試。',
      }
    } finally {
      metroStatusLoading.value = false
    }
  }

  function startMetroStatusRefresh(cityId: string, routeId: string): void {
    stopMetroStatusRefresh()
    void refreshMetroStatus(cityId, routeId)
    metroStatusRefreshTimer = window.setInterval(() => {
      void refreshMetroStatus(cityId, routeId)
    }, metroStatusRefreshMilliseconds)
  }

  function stopMetroStatusRefresh(): void {
    if (metroStatusRefreshTimer === undefined) return

    window.clearInterval(metroStatusRefreshTimer)
    metroStatusRefreshTimer = undefined
  }

  async function chooseMetroRoute(cityId: string, route: TransitRoute): Promise<void> {
    const nextRoute = selectedMetroRoute.value?.id === route.id ? null : route
    selectedMetroRoute.value = nextRoute
    selectedMetroDirection.value = nextRoute?.directions[0]?.direction ?? null
    selectedMetroRouteStations.value = []

    if (!nextRoute) {
      selectedStation.value = null
      arrivals.value = []
      return
    }

    if (
      selectedStation.value &&
      selectedStation.value.railwayId !== nextRoute.id
    ) {
      selectedStation.value = null
      arrivals.value = []
    }

    await run(async () => {
      const response = await requestMetroStations(cityId, nextRoute.id)
      if (selectedMetroRoute.value?.id !== nextRoute.id) return

      selectedMetroRouteStations.value = response.data
      resultMeta.value = response.meta
    })
  }

  async function chooseMetroStation(cityId: string, station: MetroStation): Promise<void> {
    selectedStation.value = station
    await run(async () => {
      const response = await getMetroArrivals(cityId, station.id)
      arrivals.value = response.data
      syncMetroDirection()
      resultMeta.value = response.meta
    })
  }

  function chooseMetroDirection(direction: number): void {
    selectedMetroDirection.value = direction
  }

  function syncMetroDirection(): void {
    const options = metroDirectionOptions.value
    if (
      selectedMetroDirection.value !== null &&
      options.some((item) => item.direction === selectedMetroDirection.value)
    ) {
      return
    }
    selectedMetroDirection.value = options[0]?.direction ?? null
  }

  async function refreshMetroArrivals(cityId: string): Promise<void> {
    if (!loading.value && selectedStation.value) {
      await chooseMetroStation(cityId, selectedStation.value)
    }
  }

  function startMetroArrivalRefresh(cityId: string): void {
    stopMetroArrivalRefresh()
    metroArrivalRefreshTimer = window.setInterval(() => {
      void refreshMetroArrivals(cityId)
    }, metroArrivalRefreshMilliseconds)
  }

  function stopMetroArrivalRefresh(): void {
    if (metroArrivalRefreshTimer === undefined) return

    window.clearInterval(metroArrivalRefreshTimer)
    metroArrivalRefreshTimer = undefined
  }

  async function searchRailStations(cityId: string): Promise<void> {
    await run(async () => {
      const response = await requestRailStations(cityId, railQuery.value.trim())
      railStations.value = response.data
      selectedRailStation.value = null
      selectedRailHeading.value = null
      arrivals.value = []
      resultMeta.value = response.meta
    })
  }

  async function chooseRailStation(cityId: string, station: RailStation): Promise<void> {
    const stationChanged = selectedRailStation.value?.id !== station.id
    selectedRailStation.value = station
    if (stationChanged) selectedRailHeading.value = null
    await run(async () => {
      const response = await getRailArrivals(cityId, station.id)
      arrivals.value = response.data
      syncRailHeading()
      resultMeta.value = response.meta
    })
  }

  function chooseRailHeading(heading: RailHeading): void {
    selectedRailHeading.value = heading
  }

  function syncRailHeading(): void {
    if (selectedRailHeading.value !== null) return
    selectedRailHeading.value = arrivals.value.find((item) => item.heading)?.heading ?? 'north'
  }

  async function refreshRailArrivals(cityId: string): Promise<void> {
    if (!loading.value && selectedRailStation.value) {
      await chooseRailStation(cityId, selectedRailStation.value)
    }
  }

  function startRailArrivalRefresh(cityId: string): void {
    stopRailArrivalRefresh()
    railArrivalRefreshTimer = window.setInterval(() => {
      void refreshRailArrivals(cityId)
    }, railArrivalRefreshMilliseconds)
  }

  function stopRailArrivalRefresh(): void {
    if (railArrivalRefreshTimer === undefined) return

    window.clearInterval(railArrivalRefreshTimer)
    railArrivalRefreshTimer = undefined
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
    selectedMetroRoute,
    selectedMetroDirection,
    filteredMetroStations,
    selectedMetroStatus,
    metroDirectionOptions,
    visibleMetroArrivals,
    railHeadingOptions,
    visibleRailArrivals,
    selectedRoute,
    selectedDirection,
    stops,
    selectedStop,
    selectedStation,
    selectedRailStation,
    selectedRailHeading,
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
    startBusArrivalRefresh,
    stopBusArrivalRefresh,
    searchMetroStations,
    searchRouteMetro,
    refreshMetroStatus,
    startMetroStatusRefresh,
    stopMetroStatusRefresh,
    chooseMetroRoute,
    chooseMetroDirection,
    chooseMetroStation,
    refreshMetroArrivals,
    startMetroArrivalRefresh,
    stopMetroArrivalRefresh,
    searchRailStations,
    chooseRailStation,
    chooseRailHeading,
    refreshRailArrivals,
    startRailArrivalRefresh,
    stopRailArrivalRefresh,
    loadProviderStatus,
  }
})
