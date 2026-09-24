import { apiRequest } from './client'
import type { ApiResponse, WeatherForecast } from '@/types/api'

export function getCityWeather(cityId: string): Promise<ApiResponse<WeatherForecast | null>> {
  return apiRequest(`/v1/weather?${params({ cityId })}`)
}

export function getLocationWeather(
  latitude: number,
  longitude: number,
): Promise<ApiResponse<WeatherForecast | null>> {
  return apiRequest(
    `/v1/weather/location?${params({
      lat: latitude.toFixed(4),
      lon: longitude.toFixed(4),
    })}`,
  )
}

function params(values: Record<string, string>): string {
  return new URLSearchParams(values).toString()
}
