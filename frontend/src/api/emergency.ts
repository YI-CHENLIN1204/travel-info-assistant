import { apiRequest } from './client'
import type { ApiResponse, EmergencyInfo } from '@/types/api'

export function getEmergencyInfo(cityId: string): Promise<ApiResponse<EmergencyInfo>> {
  return apiRequest(`/v1/emergency?${new URLSearchParams({ cityId }).toString()}`)
}
