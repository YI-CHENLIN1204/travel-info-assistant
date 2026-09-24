import { describe, expect, it } from 'vitest'
import { getAlertAction, getAlertTone } from '@/services/alertDisplay'

describe('alert display helpers', () => {
  it('maps official levels to stable visual tones', () => {
    expect([1, 2, 3, 4].map(getAlertTone)).toEqual(['gray', 'yellow', 'orange', 'red'])
  })

  it('uses the official action meaning for severe levels', () => {
    expect(getAlertAction(3)).toBe('避免前往')
    expect(getAlertAction(4)).toBe('儘速離境')
  })
})
