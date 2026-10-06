import { apiRequest } from './client'
import type {
  ApiResponse,
  MetroServiceStatus,
  MetroStation,
  RailServiceKey,
  RailStation,
  TdxProviderStatus,
  TransitArrival,
  TransitDepartureSchedule,
  TransitJourneySchedule,
  TransitMode,
  TransitRoute,
  TransitStop,
} from '@/types/api'

export function getTransitModes(cityId: string): Promise<ApiResponse<TransitMode[]>> {
  return apiRequest(`/v1/transit/modes?${params({ cityId })}`)
}

export function searchBusRoutes(
  cityId: string,
  query: string,
): Promise<ApiResponse<TransitRoute[]>> {
  return apiRequest(`/v1/transit/bus/routes?${params({ cityId, q: query })}`)
}

export function getBusStops(
  cityId: string,
  routeName: string,
  direction: number,
): Promise<ApiResponse<TransitStop[]>> {
  return apiRequest(
    `/v1/transit/bus/stops?${params({ cityId, routeName, direction: String(direction) })}`,
  )
}

export function getBusArrivals(
  cityId: string,
  routeName: string,
  direction: number,
  stopId: string,
): Promise<ApiResponse<TransitArrival[]>> {
  return apiRequest(
    `/v1/transit/bus/arrivals?${params({
      cityId,
      routeName,
      direction: String(direction),
      stopId,
    })}`,
  )
}

export function getTramRoutes(cityId: string): Promise<ApiResponse<TransitRoute[]>> {
  return apiRequest(`/v1/transit/tram/routes?${params({ cityId })}`)
}

export function getTramStops(
  cityId: string,
  routeId: string,
  direction: number,
): Promise<ApiResponse<TransitStop[]>> {
  return apiRequest(
    `/v1/transit/tram/stops?${params({ cityId, routeId, direction: String(direction) })}`,
  )
}

export function getTramDepartures(
  cityId: string,
  routeId: string,
  direction: number,
): Promise<ApiResponse<TransitDepartureSchedule | null>> {
  return apiRequest(
    `/v1/transit/tram/departures?${params({ cityId, routeId, direction: String(direction) })}`,
  )
}

export function getFerryRoutes(cityId: string): Promise<ApiResponse<TransitRoute[]>> {
  return apiRequest(`/v1/transit/ferry/routes?${params({ cityId })}`)
}

export function getFerryStops(
  cityId: string,
  routeId: string,
  direction: number,
): Promise<ApiResponse<TransitStop[]>> {
  return apiRequest(
    `/v1/transit/ferry/stops?${params({ cityId, routeId, direction: String(direction) })}`,
  )
}

export function getFerryJourneys(
  cityId: string,
  routeId: string,
  direction: number,
): Promise<ApiResponse<TransitJourneySchedule | null>> {
  return apiRequest(
    `/v1/transit/ferry/journeys?${params({ cityId, routeId, direction: String(direction) })}`,
  )
}

export function searchMetroStations(
  cityId: string,
  query: string,
): Promise<ApiResponse<MetroStation[]>> {
  return apiRequest(`/v1/transit/metro/stations?${params({ cityId, q: query })}`)
}

export function searchMetroRoutes(
  cityId: string,
  query: string,
): Promise<ApiResponse<TransitRoute[]>> {
  return apiRequest(`/v1/transit/metro/routes?${params({ cityId, q: query })}`)
}

export function getMetroArrivals(
  cityId: string,
  stationId: string,
): Promise<ApiResponse<TransitArrival[]>> {
  return apiRequest(`/v1/transit/metro/arrivals?${params({ cityId, stationId })}`)
}

export function getMetroStatus(
  cityId: string,
  routeId?: string,
): Promise<ApiResponse<MetroServiceStatus[]>> {
  return apiRequest(`/v1/transit/metro/status?${params({ cityId, routeId: routeId ?? '' })}`)
}

export function searchRailStations(
  cityId: string,
  query: string,
  serviceKey: RailServiceKey = 'rail',
): Promise<ApiResponse<RailStation[]>> {
  const path = serviceKey === 'high-speed-rail'
    ? '/v1/transit/rail/high-speed/stations'
    : '/v1/transit/rail/stations'
  return apiRequest(`${path}?${params({ cityId, q: query })}`)
}

export function getRailArrivals(
  cityId: string,
  stationId: string,
  serviceKey: RailServiceKey = 'rail',
): Promise<ApiResponse<TransitArrival[]>> {
  const path = serviceKey === 'high-speed-rail'
    ? '/v1/transit/rail/high-speed/arrivals'
    : '/v1/transit/rail/arrivals'
  return apiRequest(`${path}?${params({ cityId, stationId })}`)
}

export function getTdxStatus(): Promise<ApiResponse<TdxProviderStatus>> {
  return apiRequest('/v1/transit/tdx/status')
}

function params(values: Record<string, string>): string {
  return new URLSearchParams(
    Object.entries(values).filter(([, value]) => value.length > 0),
  ).toString()
}
