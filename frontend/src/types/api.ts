export interface ApiMeta {
  dataStatus: 'realtime' | 'scheduled' | 'cached' | 'unavailable'
  source: string
  sourceUpdatedAt: string | null
  fetchedAt: string
  stale: boolean
  message: string | null
}

export interface ApiResponse<T> {
  data: T
  meta: ApiMeta
}

export interface ServiceCapability {
  serviceKey: string
  displayName: string
  integrationStatus: 'integrated' | 'notIntegrated' | 'explicitlyUnsupported'
  availabilityStatus: 'available' | 'temporarilyUnavailable'
  message: string | null
}

export interface City {
  id: string
  code: string
  nameZh: string
  nameEn: string
  countryCode: string
  timeZone: string
  centerLatitude: number
  centerLongitude: number
  services: ServiceCapability[]
}

export interface ResolveCityResult {
  supported: boolean
  city: City | null
  distanceKilometers: number | null
}

export interface DependencyHealth {
  available: boolean
  status: string
}

export interface SystemHealth {
  status: 'healthy' | 'degraded'
  database: DependencyHealth
  redis: DependencyHealth
  checkedAt: string
}
