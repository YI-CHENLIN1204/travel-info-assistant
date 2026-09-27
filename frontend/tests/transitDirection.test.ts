import { getBusDirectionLabel } from '@/services/transitDirection'
import { filterTokyoMetroStations } from '@/stores/transit'
import type { MetroStation, TransitRoute } from '@/types/api'

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

  it('returns all stations when no route is selected', () => {
    expect(filterTokyoMetroStations(stations, null)).toEqual(stations)
  })
})
