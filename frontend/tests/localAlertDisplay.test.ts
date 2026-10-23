import { describe, expect, it } from 'vitest'
import { getLocalAlertKindLabel, getLocalAlertTone, getTravelerAlerts } from '@/services/localAlertDisplay'
import type { LocalAlert } from '@/types/api'

const alert = (category: string, overrides: Partial<LocalAlert> = {}): LocalAlert => ({
  id: category, category, title: category, kind: 'weather', severity: 'unknown',
  severityLabel: '官方生效中', summary: '官方內容', regionName: '台北', sourceName: '官方',
  updatedAt: '2026-10-24T10:00:00Z', effectiveAt: null, expiresAt: null,
  sourceUrl: 'https://example.com', ...overrides,
})

describe('local alert display helpers', () => {
  it('hides the requested noisy categories even when a source gives them a high severity', () => {
    expect(getTravelerAlerts(['水庫放流', '降雨', '強風', '道路封閉'].map(category =>
      alert(category, { severity: 'severe' })))).toEqual([])
  })

  it('keeps the latest typhoon bulletin and actionable train cancellations', () => {
    const result = getTravelerAlerts([
      alert('颱風', { id: 'old' }),
      alert('颱風', { id: 'new', updatedAt: '2026-10-24T11:00:00Z' }),
      alert('鐵路停駛', { kind: 'transport' }),
      alert('雷雨'),
    ])
    expect(result.map(item => item.id)).toEqual(['new', '鐵路停駛'])
  })
  it('maps normalized severities without inventing a source level', () => {
    expect(getLocalAlertTone('unknown')).toBe('gray')
    expect(getLocalAlertTone('advisory')).toBe('yellow')
    expect(getLocalAlertTone('warning')).toBe('orange')
    expect(getLocalAlertTone('severe')).toBe('red')
    expect(getLocalAlertTone('emergency')).toBe('red')
  })

  it('labels supported alert categories in Traditional Chinese', () => {
    expect(getLocalAlertKindLabel('weather')).toBe('天候警報')
    expect(getLocalAlertKindLabel('transport')).toBe('交通異常')
    expect(getLocalAlertKindLabel('publicSafety')).toBe('公共安全')
  })
})
