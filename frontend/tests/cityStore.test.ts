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

    store.selectCity('hong-kong-id')

    expect(store.selectedCityId).toBe('hong-kong-id')
    expect(window.localStorage.getItem('lastSelectedCityId')).toBe('hong-kong-id')

    store.selectCity('unknown-city-id')

    expect(store.selectedCityId).toBe('hong-kong-id')
    expect(window.localStorage.getItem('lastSelectedCityId')).toBe('hong-kong-id')
  })
})
