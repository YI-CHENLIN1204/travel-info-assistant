import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCities } from '@/api/system'
import { useCityStore } from '@/stores/city'
import type { City } from '@/types/api'

vi.mock('@/api/system', () => ({
  getCities: vi.fn(),
  resolveCity: vi.fn(),
}))

const cities: City[] = [
  {
    id: 'taipei-id',
    code: 'taipei',
    nameZh: '台北',
    nameEn: 'Taipei',
    countryCode: 'TW',
    timeZone: 'Asia/Taipei',
    centerLatitude: 25.0375,
    centerLongitude: 121.5637,
    services: [],
  },
  {
    id: 'tokyo-id',
    code: 'tokyo',
    nameZh: '東京',
    nameEn: 'Tokyo',
    countryCode: 'JP',
    timeZone: 'Asia/Tokyo',
    centerLatitude: 35.6762,
    centerLongitude: 139.6503,
    services: [],
  },
  {
    id: 'hong-kong-id',
    code: 'hong-kong',
    nameZh: '香港',
    nameEn: 'Hong Kong',
    countryCode: 'HK',
    timeZone: 'Asia/Hong_Kong',
    centerLatitude: 22.3193,
    centerLongitude: 114.1694,
    services: [],
  },
  {
    id: 'singapore-id',
    code: 'singapore',
    nameZh: '新加坡',
    nameEn: 'Singapore',
    countryCode: 'SG',
    timeZone: 'Asia/Singapore',
    centerLatitude: 1.3521,
    centerLongitude: 103.8198,
    services: [],
  },
]

const mockedGetCities = vi.mocked(getCities)

describe('city store persistence', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.localStorage.clear()
    vi.resetAllMocks()
    mockedGetCities.mockResolvedValue(cities)
  })

  it('restores a previously selected city that is still available', async () => {
    window.localStorage.setItem('lastSelectedCityId', 'tokyo-id')

    const store = useCityStore()
    await store.initialize()

    expect(store.selectedCityId).toBe('tokyo-id')
    expect(store.currentCity.code).toBe('tokyo')
  })

  it('falls back to Taipei when the stored city is no longer available', async () => {
    window.localStorage.setItem('lastSelectedCityId', 'removed-city-id')

    const store = useCityStore()
    await store.initialize()

    expect(store.selectedCityId).toBe('taipei-id')
    expect(store.currentCity.code).toBe('taipei')
  })

  it('persists only available city selections', async () => {
    const store = useCityStore()
    await store.initialize()

    store.selectCity('singapore-id')

    expect(store.selectedCityId).toBe('singapore-id')
    expect(store.currentCity.timeZone).toBe('Asia/Singapore')
    expect(window.localStorage.getItem('lastSelectedCityId')).toBe('singapore-id')

    store.selectCity('unknown-city-id')

    expect(store.selectedCityId).toBe('singapore-id')
    expect(window.localStorage.getItem('lastSelectedCityId')).toBe('singapore-id')
  })
})
