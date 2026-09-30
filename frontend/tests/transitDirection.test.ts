import { createPinia, setActivePinia } from 'pinia'
import * as transitApi from '@/api/transit'
import { getBusDirectionLabel } from '@/services/transitDirection'
import {
  filterTokyoMetroStations,
  findTokyoMetroStatus,
  useTransitStore,
} from '@/stores/transit'
import type {
  ApiMeta,
  MetroServiceStatus,
  MetroStation,
  TransitArrival,
  TransitRoute,
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

describe('filterTokyoMetroStations', () => {
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
      filterTokyoMetroStations(stations, { ...route, id: 'railway-ginza' }).map(
        (station) => station.id,
      ),
    ).toEqual(['station-1', 'station-2'])
  })

  it('orders selected-route stations by the route station list', () => {
    expect(
      filterTokyoMetroStations(stations, {
        ...route,
        id: 'railway-ginza',
        stationNames: ['表參道', '銀座'],
      }).map((station) => station.id),
    ).toEqual(['station-2', 'station-1'])
  })

  it('returns all stations when no route is selected', () => {
    expect(filterTokyoMetroStations(stations, null)).toEqual(stations)
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

    await store.chooseTokyoRoute('tokyo-id', ginzaRoute)

    expect(stationSearch).toHaveBeenCalledWith('tokyo-id', 'railway-ginza')
    expect(store.filteredTokyoStations.map((station) => station.id)).toEqual([
      'station-2',
      'station-1',
    ])

    await store.chooseTokyoRoute('tokyo-id', ginzaRoute)

    expect(stationSearch).toHaveBeenCalledTimes(1)
    expect(store.filteredTokyoStations).toEqual([stations[2]])
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

    store.selectedTokyoRoute = ginzaRoute
    store.selectedStation = stations[0]
    store.arrivals = [arrival]
    await store.chooseTokyoRoute('tokyo-id', hibiyaRoute)

    expect(store.selectedStation).toBeNull()
    expect(store.arrivals).toEqual([])

    store.selectedTokyoRoute = ginzaRoute
    store.selectedStation = stations[2]
    store.arrivals = [arrival]
    await store.chooseTokyoRoute('tokyo-id', hibiyaRoute)

    expect(store.selectedStation).toEqual(stations[2])
    expect(store.arrivals).toEqual([arrival])
  })
})

describe('findTokyoMetroStatus', () => {
  const statuses: MetroServiceStatus[] = [
    {
      id: 'status-ginza',
      lineId: 'railway-ginza',
      lineName: '銀座線',
      messageJa: '現在、平常通り運転しています。',
      messageEn: 'Normal service.',
      updatedAt: '2026-09-30T12:00:00+09:00',
      validUntil: '2026-09-30T12:05:00+09:00',
    },
    {
      id: 'status-hibiya',
      lineId: 'railway-hibiya',
      lineName: '日比谷線',
      messageJa: '列車に遅れが出ています。',
      messageEn: 'Trains are delayed.',
      updatedAt: '2026-09-30T12:00:00+09:00',
      validUntil: '2026-09-30T12:05:00+09:00',
    },
  ]

  it('returns only the status belonging to the selected route', () => {
    expect(
      findTokyoMetroStatus(statuses, { ...route, id: 'railway-ginza' })?.id,
    ).toBe('status-ginza')
    expect(
      findTokyoMetroStatus(statuses, { ...route, id: 'railway-hibiya' })?.id,
    ).toBe('status-hibiya')
  })

  it('returns no status when no route is selected or the line has no status', () => {
    expect(findTokyoMetroStatus(statuses, null)).toBeNull()
    expect(
      findTokyoMetroStatus(statuses, { ...route, id: 'railway-marunouchi' }),
    ).toBeNull()
  })
})
