import { apiRequest } from './client'
import type {
  ApiResponse,
  MetroStation,
  RailStation,
  TdxProviderStatus,
  TransitArrival,
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

export function searchMetroStations(
  cityId: string,
  query: string,
): Promise<ApiResponse<MetroStation[]>> {
  return apiRequest(`/v1/transit/metro/stations?${params({ cityId, q: query })}`)
}

export function getMetroArrivals(
  cityId: string,
  stationId: string,
): Promise<ApiResponse<TransitArrival[]>> {
  return apiRequest(`/v1/transit/metro/arrivals?${params({ cityId, stationId })}`)
}

export function searchRailStations(
  cityId: string,
  query: string,
): Promise<ApiResponse<RailStation[]>> {
  return apiRequest(`/v1/transit/rail/stations?${params({ cityId, q: query })}`)
}

export function getRailArrivals(
  cityId: string,
  stationId: string,
): Promise<ApiResponse<TransitArrival[]>> {
  return apiRequest(`/v1/transit/rail/arrivals?${params({ cityId, stationId })}`)
}

export function getTdxStatus(): Promise<ApiResponse<TdxProviderStatus>> {
  return apiRequest('/v1/transit/tdx/status')
}

function params(values: Record<string, string>): string {
  return new URLSearchParams(values).toString()
}
