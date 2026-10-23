import { apiRequest } from './client'
import type { ApiResponse, LocalAlert, TravelAlert } from '@/types/api'

export function getLocalAlerts(cityId: string): Promise<ApiResponse<LocalAlert[]>> {
  return apiRequest(`/v1/alerts/local?${new URLSearchParams({ cityId }).toString()}`)
}

export function getCityAlerts(cityId: string): Promise<ApiResponse<TravelAlert[]>> {
  return apiRequest(`/v1/alerts?${new URLSearchParams({ cityId }).toString()}`)
}

export function getPopularAlerts(): Promise<ApiResponse<TravelAlert[]>> {
  return apiRequest('/v1/alerts/popular')
}
