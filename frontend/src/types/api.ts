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

export interface TransitMode {
  key: 'bus' | 'metro' | 'rail'
  displayName: string
  availabilityStatus: 'available' | 'temporarilyUnavailable'
  message: string | null
}

export interface TransitDirection {
  direction: number
  headsign: string | null
  originName: string | null
  destinationName: string | null
}

export interface TransitRoute {
  id: string
  nameZh: string
  nameEn: string | null
  originName: string | null
  destinationName: string | null
  operators: string[]
  directions: TransitDirection[]
}

export interface TransitStop {
  id: string
  nameZh: string
  nameEn: string | null
  sequence: number
  direction: number
  latitude: number | null
  longitude: number | null
}

export interface MetroStation {
  id: string
  nameZh: string
  nameEn: string | null
  address: string | null
  latitude: number | null
  longitude: number | null
}

export interface RailStation {
  id: string
  nameZh: string
  nameEn: string | null
  address: string | null
  latitude: number | null
  longitude: number | null
}

export interface TransitArrival {
  id: string
  mode: 'bus' | 'metro' | 'rail'
  stopId: string
  stopName: string
  routeId: string | null
  routeName: string | null
  lineId: string | null
  lineName: string | null
  destinationName: string | null
  direction: number | null
  scheduledAt: string | null
  estimatedAt: string | null
  sourceUpdatedAt: string | null
  serviceStatus: string
  isLastService: boolean
  platform: string | null
}

export interface TdxProviderStatus {
  configured: boolean
  billingCycle: string
  requestCount: number
  responseBytes: number
  estimatedPoints: number
  softLimitPoints: number
  hardLimitPoints: number
  requestsPerMinute: number
  overageEnabled: boolean
  pricingVerifiedAt: string
}
