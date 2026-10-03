import { createPinia, setActivePinia } from 'pinia'
import * as transitApi from '@/api/transit'
import { getBusDirectionLabel } from '@/services/transitDirection'
import {
  buildArrivalDirectionOptions,
  buildFixedDirectionOptions,
  busArrivalRefreshMilliseconds,
  filterMetroRouteStations,
  findMetroRouteStatus,
  isMetroStatusCurrent,
  metroArrivalRefreshMilliseconds,
  metroStatusRefreshMilliseconds,
  railArrivalRefreshMilliseconds,
  useTransitStore,
} from '@/stores/transit'
import type {
  ApiMeta,
  MetroServiceStatus,
  MetroStation,
  RailStation,
  TransitArrival,
  TransitRoute,
  TransitStop,
} from '@/types/api'

const route: TransitRoute = {
  id: 'TPE214',
  nameZh: '214',
  nameEn: '214',
  originName: '中和',
  destinationName: '內湖',
  operators: ['中興巴士'],
  stationNames: [],
  directions: [
    {
      direction: 0,
      headsign: '214',
      originName: '中和',
      destinationName: '內湖',
    },
    {
      direction: 1,
      headsign: '214',
      originName: '內湖',
      destinationName: '中和',
    },
  ],
}

const odptMeta: ApiMeta = {
  dataStatus: 'scheduled',
  source: 'ODPT',
  sourceUpdatedAt: '2026-09-29T00:00:00+09:00',
  fetchedAt: '2026-09-29T00:01:00+09:00',
  stale: false,
  message: null,
}

afterEach(() => {
  vi.restoreAllMocks()
  vi.useRealTimers()
})

describe('getBusDirectionLabel', () => {
  it('uses each direction destination before a duplicated headsign', () => {
    expect(getBusDirectionLabel(route, 0)).toBe('往 內湖')
    expect(getBusDirectionLabel(route, 1)).toBe('往 中和')
  })

  it('falls back to outbound and return labels when metadata is missing', () => {
    expect(getBusDirectionLabel({ ...route, directions: [] }, 0)).toBe('去程')
    expect(getBusDirectionLabel({ ...route, directions: [] }, 1)).toBe('返程')
  })
})

describe('filterMetroRouteStations', () => {
  const stations: MetroStation[] = [
    {
      id: 'station-1',
      nameZh: '銀座',
      nameEn: 'Ginza',
      address: null,
      latitude: null,
      longitude: null,
      code: 'G09',
      railwayId: 'railway-ginza',
      railwayName: '銀座線',
    },
    {
      id: 'station-2',
      nameZh: '表參道',
      nameEn: 'Omote-sando',
      address: null,
      latitude: null,
      longitude: null,
      code: 'G02',
      railwayId: 'railway-ginza',
      railwayName: '銀座線',
    },
    {
      id: 'station-3',
      nameZh: '六本木',
      nameEn: 'Roppongi',
      address: null,
      latitude: null,
      longitude: null,
      code: 'H04',
      railwayId: 'railway-hibiya',
      railwayName: '日比谷線',
    },
  ]

  it('returns stations belonging to the selected route', () => {
    expect(
      filterMetroRouteStations(stations, { ...route, id: 'railway-ginza' }).map(
        (station) => station.id,
      ),
    ).toEqual(['station-1', 'station-2'])
  })

  it('uses the same route filtering for Toei Subway stations', () => {
    const toeiStation: MetroStation = {
      ...stations[0],
      id: 'station-asakusa',
      nameZh: '浅草',
      code: 'A18',
      railwayId: 'odpt.Railway:Toei.Asakusa',
      railwayName: '浅草線',
    }

    expect(
      filterMetroRouteStations([...stations, toeiStation], {
        ...route,
        id: 'odpt.Railway:Toei.Asakusa',
        stationNames: ['浅草'],
      }),
    ).toEqual([toeiStation])
  })

  it('orders selected-route stations by the route station list', () => {
    expect(
      filterMetroRouteStations(stations, {
        ...route,
        id: 'railway-ginza',
        stationNames: ['表參道', '銀座'],
      }).map((station) => station.id),
    ).toEqual(['station-2', 'station-1'])
  })

  it('reverses the station order for the opposite destination', () => {
    expect(
      filterMetroRouteStations(
        stations,
        {
          ...route,
          id: 'railway-ginza',
          stationNames: [stations[0].nameZh, stations[1].nameZh],
          directions: [
            {
              direction: 0,
              headsign: stations[1].nameZh,
              originName: stations[0].nameZh,
              destinationName: stations[1].nameZh,
            },
            {
              direction: 1,
              headsign: stations[0].nameZh,
              originName: stations[1].nameZh,
              destinationName: stations[0].nameZh,
            },
          ],
        },
        1,
      ).map((station) => station.id),
    ).toEqual(['station-2', 'station-1'])
  })

  it('returns all stations when no route is selected', () => {
    expect(filterMetroRouteStations(stations, null)).toEqual(stations)
  })

  it('loads and orders the complete station list for the selected route', async () => {
    setActivePinia(createPinia())
    const store = useTransitStore()
    const ginzaRoute = {
      ...route,
      id: 'railway-ginza',
      stationNames: ['表參道', '銀座'],
    }
    store.stations = [stations[2]]
    const stationSearch = vi.spyOn(transitApi, 'searchMetroStations').mockResolvedValue({
      data: [stations[0], stations[1]],
      meta: odptMeta,
    })

    await store.chooseMetroRoute('tokyo-id', ginzaRoute)

    expect(stationSearch).toHaveBeenCalledWith('tokyo-id', 'railway-ginza')
    expect(store.filteredMetroStations.map((station) => station.id)).toEqual([
      'station-2',
      'station-1',
    ])

    store.selectedStation = stations[0]
    store.arrivals = [{ id: 'arrival-1' } as TransitArrival]
    await store.chooseMetroRoute('tokyo-id', ginzaRoute)

    expect(stationSearch).toHaveBeenCalledTimes(1)
    expect(store.filteredMetroStations).toEqual([stations[2]])
    expect(store.selectedStation).toBeNull()
    expect(store.arrivals).toEqual([])
  })

  it('clears only a station that does not belong to the newly selected route', async () => {
    setActivePinia(createPinia())
    const store = useTransitStore()
    const ginzaRoute = { ...route, id: 'railway-ginza' }
    const hibiyaRoute = { ...route, id: 'railway-hibiya' }
    const arrival = { id: 'arrival-1' } as TransitArrival
    vi.spyOn(transitApi, 'searchMetroStations').mockResolvedValue({
      data: [stations[2]],
      meta: odptMeta,
    })

    store.selectedMetroRoute = ginzaRoute
    store.selectedStation = stations[0]
    store.arrivals = [arrival]
    await store.chooseMetroRoute('tokyo-id', hibiyaRoute)

    expect(store.selectedStation).toBeNull()
    expect(store.arrivals).toEqual([])

    store.selectedMetroRoute = ginzaRoute
    store.selectedStation = stations[2]
    store.arrivals = [arrival]
    await store.chooseMetroRoute('tokyo-id', hibiyaRoute)

    expect(store.selectedStation).toEqual(stations[2])
    expect(store.arrivals).toEqual([arrival])
  })

  it('derives Taipei direction options and hides the opposite direction arrivals', async () => {
    setActivePinia(createPinia())
    const store = useTransitStore()
    const directionZero = {
      id: 'arrival-0',
      direction: 0,
      destinationName: '淡水',
    } as TransitArrival
    const directionOne = {
      id: 'arrival-1',
      direction: 1,
      destinationName: '象山',
    } as TransitArrival
    vi.spyOn(transitApi, 'getMetroArrivals').mockResolvedValue({
      data: [directionZero, directionOne],
      meta: { ...odptMeta, source: 'TDX', dataStatus: 'realtime' },
    })
    vi.spyOn(transitApi, 'getTdxStatus').mockRejectedValue(new Error('not needed'))

    await store.chooseMetroStation('taipei-id', stations[0])

    expect(store.metroDirectionOptions.map((item) => item.direction)).toEqual([0, 1])
    expect(store.selectedMetroDirection).toBe(0)
    expect(store.visibleMetroArrivals).toEqual([directionZero])

    store.chooseMetroDirection(1)
    expect(store.visibleMetroArrivals).toEqual([directionOne])
  })

  it('keeps route direction buttons fixed when a short-turn train arrives', () => {
    setActivePinia(createPinia())
    const store = useTransitStore()
    store.selectedMetroRoute = {
      ...route,
      id: 'MTR:TKL',
      directions: [
        {
          direction: 0,
          headsign: '北角',
          originName: '寶琳／康城',
          destinationName: '北角',
        },
        {
          direction: 1,
          headsign: '寶琳／康城',
          originName: '北角',
          destinationName: '寶琳／康城',
        },
      ],
    }
    store.arrivals = [
      { direction: 0, destinationName: '調景嶺' } as TransitArrival,
      { direction: 1, destinationName: '調景嶺' } as TransitArrival,
    ]

    expect(store.metroDirectionOptions).toEqual([
      expect.objectContaining({
        direction: 0,
        headsign: '北角',
        destinationName: '北角',
      }),
      expect.objectContaining({
        direction: 1,
        headsign: '寶琳／康城',
        destinationName: '寶琳／康城',
      }),
    ])
    store.chooseMetroDirection(0)
    expect(store.visibleMetroArrivals[0]?.destinationName).toBe('調景嶺')
  })
})

describe('arrival direction controls', () => {
  it('combines unique destinations under their provider direction', () => {
    expect(
      buildArrivalDirectionOptions([
        { direction: 0, destinationName: '北角' } as TransitArrival,
        { direction: 1, destinationName: '寶琳' } as TransitArrival,
        { direction: 1, destinationName: '康城' } as TransitArrival,
        { direction: 1, destinationName: '寶琳' } as TransitArrival,
      ]),
    ).toEqual([
      expect.objectContaining({ direction: 0, destinationName: '北角' }),
      expect.objectContaining({ direction: 1, destinationName: '寶琳／康城' }),
    ])
  })

  it('builds stable route-less direction controls without trip destinations', () => {
    expect(
      buildFixedDirectionOptions([
        { direction: 1, destinationName: '花蓮' } as TransitArrival,
        { direction: 0, destinationName: '高雄' } as TransitArrival,
        { direction: 1, destinationName: '臺東' } as TransitArrival,
      ]),
    ).toEqual([
      expect.objectContaining({ direction: 0, destinationName: null }),
      expect.objectContaining({ direction: 1, destinationName: null }),
    ])
  })

  it('filters Taiwan Rail arrivals by direction and destination', async () => {
    setActivePinia(createPinia())
    const store = useTransitStore()
    const station = { id: '1000', nameZh: '臺北' } as RailStation
    const northbound = {
      id: 'rail-0',
      direction: 0,
      destinationName: '花蓮',
      heading: 'north',
    } as TransitArrival
    const southbound = {
      id: 'rail-1',
      direction: 1,
      destinationName: '高雄',
      heading: 'south',
    } as TransitArrival
    vi.spyOn(transitApi, 'getRailArrivals').mockResolvedValue({
      data: [southbound, northbound],
      meta: { ...odptMeta, source: 'TDX', dataStatus: 'realtime' },
    })
    vi.spyOn(transitApi, 'getTdxStatus').mockRejectedValue(new Error('not needed'))

    await store.chooseRailStation('taipei-id', station)

    expect(store.railDirectionOptions.map((item) => item.destinationName)).toEqual([
      null,
      null,
    ])
    expect(store.selectedRailDirection).toBe(0)
    expect(store.visibleRailArrivals).toEqual([northbound])

    store.chooseRailDirection(1)
    expect(store.visibleRailArrivals).toEqual([southbound])
  })

  it('refreshes selected bus and rail arrivals on their intervals', async () => {
    vi.useFakeTimers()
    setActivePinia(createPinia())
    const store = useTransitStore()
    const busStop = { id: 'stop-1', nameZh: '台北車站' } as TransitStop
    const railStation = { id: '1000', nameZh: '臺北' } as RailStation
    const busRequest = vi.spyOn(transitApi, 'getBusArrivals').mockResolvedValue({
      data: [],
      meta: { ...odptMeta, source: 'TDX', dataStatus: 'realtime' },
    })
    const railRequest = vi.spyOn(transitApi, 'getRailArrivals').mockResolvedValue({
      data: [],
      meta: { ...odptMeta, source: 'TDX', dataStatus: 'realtime' },
    })
    vi.spyOn(transitApi, 'getTdxStatus').mockRejectedValue(new Error('not needed'))

    store.selectedRoute = route
    store.selectedStop = busStop
    store.startBusArrivalRefresh('taipei-id')
    await vi.advanceTimersByTimeAsync(busArrivalRefreshMilliseconds)
    expect(busRequest).toHaveBeenCalledWith('taipei-id', route.nameZh, 0, 'stop-1')
    store.stopBusArrivalRefresh()

    store.selectedRailStation = railStation
    store.startRailArrivalRefresh('taipei-id')
    await vi.advanceTimersByTimeAsync(railArrivalRefreshMilliseconds)
    expect(railRequest).toHaveBeenCalledWith('taipei-id', railStation.id)
    store.stopRailArrivalRefresh()
  })
})

describe('findMetroRouteStatus', () => {
  const statuses: MetroServiceStatus[] = [
    {
      id: 'status-ginza',
      lineId: 'railway-ginza',
      lineName: '銀座線',
      messageJa: '現在、平常通り運転しています。',
      messageEn: 'Normal service.',
      messageZh: null,
      updatedAt: '2026-09-30T12:00:00+09:00',
      validUntil: '2026-09-30T12:05:00+09:00',
    },
    {
      id: 'status-hibiya',
      lineId: 'railway-hibiya',
      lineName: '日比谷線',
      messageJa: '列車に遅れが出ています。',
      messageEn: 'Trains are delayed.',
      messageZh: null,
      updatedAt: '2026-09-30T12:00:00+09:00',
      validUntil: '2026-09-30T12:05:00+09:00',
    },
    {
      id: 'status-toei-asakusa',
      lineId: 'odpt.Railway:Toei.Asakusa',
      lineName: '浅草線',
      messageJa: '現在、平常通り運転しています。',
      messageEn: 'Normal service.',
      messageZh: null,
      updatedAt: '2026-09-30T12:00:00+09:00',
      validUntil: '2026-09-30T12:05:00+09:00',
    },
  ]

  it('returns only the status belonging to the selected route', () => {
    expect(
      findMetroRouteStatus(statuses, { ...route, id: 'railway-ginza' })?.id,
    ).toBe('status-ginza')
    expect(
      findMetroRouteStatus(statuses, { ...route, id: 'railway-hibiya' })?.id,
    ).toBe('status-hibiya')
    expect(
      findMetroRouteStatus(statuses, {
        ...route,
        id: 'odpt.Railway:Toei.Asakusa',
      })?.id,
    ).toBe('status-toei-asakusa')
  })

  it('returns no status when no route is selected or the line has no status', () => {
    expect(findMetroRouteStatus(statuses, null)).toBeNull()
    expect(
      findMetroRouteStatus(statuses, { ...route, id: 'railway-marunouchi' }),
    ).toBeNull()
  })

  it('rejects a status when its official validity has expired', () => {
    expect(
      isMetroStatusCurrent(
        statuses[0],
        new Date('2026-09-30T12:04:59+09:00'),
      ),
    ).toBe(true)
    expect(
      isMetroStatusCurrent(
        statuses[0],
        new Date('2026-09-30T12:05:00+09:00'),
      ),
    ).toBe(false)
    expect(
      isMetroStatusCurrent(
        { ...statuses[0], validUntil: null },
        new Date('2026-09-30T12:04:00+09:00'),
      ),
    ).toBe(false)
  })

  it('refreshes only selected-route status on its interval and stops on request', async () => {
    vi.useFakeTimers()
    setActivePinia(createPinia())
    const store = useTransitStore()
    const ginzaRoute = { ...route, id: 'railway-ginza' }
    const selectedStation = {
      id: 'station-1',
      nameZh: '銀座',
    } as MetroStation
    const arrival = { id: 'arrival-1' } as TransitArrival
    const statusRequest = vi.spyOn(transitApi, 'getMetroStatus').mockResolvedValue({
      data: statuses,
      meta: { ...odptMeta, dataStatus: 'realtime' },
    })
    store.selectedMetroRoute = ginzaRoute
    store.selectedStation = selectedStation
    store.arrivals = [arrival]

    store.startMetroStatusRefresh('tokyo-id', ginzaRoute.id)
    await vi.advanceTimersByTimeAsync(0)

    expect(statusRequest).toHaveBeenCalledTimes(1)
    expect(statusRequest).toHaveBeenLastCalledWith('tokyo-id', 'railway-ginza')
    expect(store.selectedMetroRoute).toEqual(ginzaRoute)
    expect(store.selectedStation).toEqual(selectedStation)
    expect(store.arrivals).toEqual([arrival])

    await vi.advanceTimersByTimeAsync(metroStatusRefreshMilliseconds)
    expect(statusRequest).toHaveBeenCalledTimes(2)

    store.stopMetroStatusRefresh()
    await vi.advanceTimersByTimeAsync(metroStatusRefreshMilliseconds)
    expect(statusRequest).toHaveBeenCalledTimes(2)
  })

  it('uses the Singapore source when an LTA status refresh fails', async () => {
    setActivePinia(createPinia())
    const store = useTransitStore()
    vi.spyOn(transitApi, 'getMetroStatus').mockRejectedValue(new Error('unavailable'))

    await store.refreshMetroStatus('singapore-id', 'LTA:NS')

    expect(store.metroStatusMeta).toMatchObject({
      dataStatus: 'unavailable',
      source: '新加坡 LTA DataMall',
      stale: true,
    })
  })

  it('refreshes Hong Kong arrivals on its interval and stops on request', async () => {
    vi.useFakeTimers()
    setActivePinia(createPinia())
    const store = useTransitStore()
    const station = {
      id: 'MTR:TWL:TST',
      nameZh: '尖沙咀',
    } as MetroStation
    const arrival = {
      id: 'mtr-arrival-1',
      estimatedAt: '2026-10-01T12:03:00+08:00',
    } as TransitArrival
    const arrivalRequest = vi.spyOn(transitApi, 'getMetroArrivals').mockResolvedValue({
      data: [arrival],
      meta: { ...odptMeta, source: '香港港鐵開放數據', dataStatus: 'realtime' },
    })
    vi.spyOn(transitApi, 'getTdxStatus').mockRejectedValue(new Error('not needed'))
    store.selectedStation = station

    store.startMetroArrivalRefresh('hong-kong-id')
    await vi.advanceTimersByTimeAsync(metroArrivalRefreshMilliseconds)

    expect(arrivalRequest).toHaveBeenCalledWith('hong-kong-id', station.id)
    expect(store.arrivals).toEqual([arrival])

    store.stopMetroArrivalRefresh()
    await vi.advanceTimersByTimeAsync(metroArrivalRefreshMilliseconds)
    expect(arrivalRequest).toHaveBeenCalledTimes(1)
  })
})
