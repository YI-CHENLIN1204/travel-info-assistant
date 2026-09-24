import { getWeatherIconKind, getWindDirection } from '@/services/weatherDisplay'

describe('weatherDisplay', () => {
  it('maps MET Norway condition codes to display icon groups', () => {
    expect(getWeatherIconKind('clearsky')).toBe('clear')
    expect(getWeatherIconKind('partlycloudy')).toBe('partly-cloudy')
    expect(getWeatherIconKind('heavyrainandthunder')).toBe('thunder')
    expect(getWeatherIconKind('lightsnowshowers')).toBe('snow')
  })

  it('formats wind direction into eight compass sectors', () => {
    expect(getWindDirection(0)).toBe('北風')
    expect(getWindDirection(90)).toBe('東風')
    expect(getWindDirection(225)).toBe('西南風')
    expect(getWindDirection(null)).toBe('尚未提供')
  })
})
