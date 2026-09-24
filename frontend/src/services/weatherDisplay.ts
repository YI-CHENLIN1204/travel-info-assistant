export type WeatherIconKind = 'clear' | 'partly-cloudy' | 'cloudy' | 'rain' | 'snow' | 'thunder' | 'fog'

export function getWeatherIconKind(conditionCode: string): WeatherIconKind {
  const code = conditionCode.toLowerCase()
  if (code.includes('thunder')) return 'thunder'
  if (code.includes('snow') || code.includes('sleet')) return 'snow'
  if (code.includes('rain')) return 'rain'
  if (code.includes('fog')) return 'fog'
  if (code.includes('partlycloudy') || code.includes('fair')) return 'partly-cloudy'
  if (code.includes('cloudy')) return 'cloudy'
  return 'clear'
}

export function getWindDirection(degrees: number | null): string {
  if (degrees === null || !Number.isFinite(degrees)) return '尚未提供'
  const directions = ['北', '東北', '東', '東南', '南', '西南', '西', '西北']
  const normalized = ((degrees % 360) + 360) % 360
  return `${directions[Math.round(normalized / 45) % directions.length]}風`
}
