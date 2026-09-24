import { describe, expect, it } from 'vitest'
import {
  formatVerifiedDate,
  getEmergencyCategoryLabel,
  getEmergencyCategoryTone,
} from '@/services/emergencyDisplay'

describe('emergency display helpers', () => {
  it('maps known contact categories', () => {
    expect(getEmergencyCategoryLabel('fireMedical')).toBe('消防與救護')
    expect(getEmergencyCategoryTone('mofaEmergency')).toBe('violet')
  })

  it('keeps unknown categories usable', () => {
    expect(getEmergencyCategoryLabel('other')).toBe('緊急聯絡')
    expect(getEmergencyCategoryTone('other')).toBe('slate')
  })

  it('formats the manual verification date without timezone conversion', () => {
    expect(formatVerifiedDate('2026-09-24')).toBe('2026/09/24')
  })
})
