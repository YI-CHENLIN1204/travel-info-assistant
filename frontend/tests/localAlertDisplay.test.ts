import { describe, expect, it } from 'vitest'
import { getLocalAlertKindLabel, getLocalAlertTone } from '@/services/localAlertDisplay'

describe('local alert display helpers', () => {
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
