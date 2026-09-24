import { apiRequest } from './client'
import type { ApiResponse, TravelAlert } from '@/types/api'

export function getCityAlerts(cityId: string): Promise<ApiResponse<TravelAlert[]>> {
  return apiRequest(`/v1/alerts?${new URLSearchParams({ cityId }).toString()}`)
}

export function getPopularAlerts(): Promise<ApiResponse<TravelAlert[]>> {
  return apiRequest('/v1/alerts/popular')
}
