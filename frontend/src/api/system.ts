import { apiRequest } from './client'
import type { City, ResolveCityResult, SystemHealth } from '@/types/api'

export async function getCities(): Promise<City[]> {
  const response = await apiRequest<City[]>('/v1/cities')
  return response.data
}

export async function resolveCity(latitude: number, longitude: number): Promise<ResolveCityResult> {
  const response = await apiRequest<ResolveCityResult>('/v1/location/resolve-city', {
    method: 'POST',
    body: JSON.stringify({ latitude, longitude }),
  })
  return response.data
}

export async function getSystemHealth(): Promise<SystemHealth> {
  const response = await apiRequest<SystemHealth>('/v1/health')
  return response.data
}
