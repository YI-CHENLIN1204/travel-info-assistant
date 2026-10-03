import { describe, expect, it } from 'vitest'
import {
  findIntegratedRailService,
  getRailDirectionLabel,
  getRailDirectionHeading,
  getRailNavigationLabel,
  getRailRegions,
  orderRailStations,
} from '@/services/railStationNavigation'
import type { RailStation, ServiceCapability, TransitDirection } from '@/types/api'

describe('rail navigation', () => {
  it('shows only an integrated rail capability', () => {
    const services: ServiceCapability[] = [
      {
        serviceKey: 'rail',
        displayName: '台鐵',
        integrationStatus: 'notIntegrated',
        availabilityStatus: 'available',
        message: null,
      },
      {
        serviceKey: 'metro',
        displayName: '地鐵',
        integrationStatus: 'integrated',
        availabilityStatus: 'available',
        message: null,
      },
    ]

    expect(findIntegratedRailService(services)).toBeNull()

    services[0]!.integrationStatus = 'integrated'
    expect(findIntegratedRailService(services)?.displayName).toBe('台鐵')
  })

  it('uses a Taiwan-specific label and preserves other countries rail names', () => {
    expect(getRailNavigationLabel('TW', '台鐵')).toBe('台鐵查詢')
    expect(getRailNavigationLabel('JP', 'JR 東日本')).toBe('JR 東日本查詢')
    expect(getRailNavigationLabel('GB', null)).toBe('鐵路查詢')
  })

  it('provides Taiwan region and locality buttons without a station text query', () => {
    const regions = getRailRegions('TW')

    expect(regions.map((region) => region.name)).toEqual([
      '北台灣',
      '中台灣',
      '南台灣',
      '東台灣',
    ])
    expect(regions.find((region) => region.id === 'central')?.localities).toContainEqual({
      name: '臺中市',
      query: '臺中市',
    })
    expect(getRailRegions('JP')).toEqual([])
  })

  it('uses arrival headings instead of provider direction numbers for Taiwan labels', () => {
    const directionZero = { direction: 0 } as TransitDirection
    const directionOne = { direction: 1 } as TransitDirection
    const arrivals = [
      { direction: 0, destinationName: '基隆', heading: 'north' as const },
      { direction: 1, destinationName: '潮州', heading: 'south' as const },
    ]

    expect(getRailDirectionLabel('TW', directionZero, arrivals)).toBe('往北')
    expect(getRailDirectionLabel('TW', directionOne, arrivals)).toBe('往南')
    expect(getRailDirectionHeading(arrivals, 0)).toBe('north')
    expect(getRailDirectionHeading(arrivals, 1)).toBe('south')
    expect(
      getRailDirectionLabel('JP', {
        direction: 0,
        destinationName: '東京',
      } as TransitDirection),
    ).toBe('往 東京')
  })

  it('falls back to actual destinations when a cardinal heading is unavailable', () => {
    expect(
      getRailDirectionLabel('TW', { direction: 1 } as TransitDirection, [
        { direction: 1, destinationName: '潮州', heading: null },
        { direction: 1, destinationName: '屏東', heading: null },
      ]),
    ).toBe('往 潮州／屏東')
  })

  it('orders Taiwan Rail stations by official line sequence for each direction', () => {
    const stations = [
      railStation('3480', '斗南', 48),
      railStation('3450', '林內', 45),
      railStation('3470', '斗六', 47),
      railStation('3460', '石榴', 46),
    ]

    expect(orderRailStations(stations, 'south').map((station) => station.id)).toEqual([
      '3450',
      '3460',
      '3470',
      '3480',
    ])
    expect(orderRailStations(stations, 'north').map((station) => station.id)).toEqual([
      '3480',
      '3470',
      '3460',
      '3450',
    ])
  })

  it('keeps provider order for stations without official line positions', () => {
    const stations = [railStation('A', '甲', null), railStation('B', '乙', null)]

    expect(orderRailStations(stations, null)).toEqual(stations)
    expect(orderRailStations(stations, 'north')).toEqual(stations)
  })
})

function railStation(id: string, nameZh: string, sequence: number | null): RailStation {
  return {
    id,
    nameZh,
    nameEn: null,
    address: null,
    latitude: sequence === null ? null : 24 - sequence / 100,
    longitude: null,
    linePositions:
      sequence === null
        ? []
        : [{ lineId: 'WL', sequence, cumulativeDistance: null }],
  }
}
