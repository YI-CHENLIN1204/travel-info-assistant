import { apiRequest } from './client'
import type {
  AeroDataBoxProviderStatus,
  Airport,
  ApiResponse,
  FlightItinerary,
} from '@/types/api'

export function searchAirports(query: string, limit = 10): Promise<ApiResponse<Airport[]>> {
  return apiRequest(`/v1/airports?${params({ q: query, limit: String(limit) })}`)
}

export function searchFlights(
  origin: string,
  destination: string,
  date: string,
): Promise<ApiResponse<FlightItinerary[]>> {
  return apiRequest(`/v1/flights/search?${params({ origin, destination, date })}`)
}

export function getFlight(
  flightNumber: string,
  date: string,
): Promise<ApiResponse<FlightItinerary[]>> {
  return apiRequest(`/v1/flights/${encodeURIComponent(flightNumber)}?${params({ date })}`)
}

export function getAeroDataBoxStatus(): Promise<ApiResponse<AeroDataBoxProviderStatus>> {
  return apiRequest('/v1/flights/provider/status')
}

function params(values: Record<string, string>): string {
  return new URLSearchParams(values).toString()
}
