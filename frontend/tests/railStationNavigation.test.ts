import { describe, expect, it } from 'vitest'
import {
  findIntegratedRailService,
  getRailNavigationLabel,
  getRailRegions,
} from '@/services/railStationNavigation'
import type { ServiceCapability } from '@/types/api'

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
})
