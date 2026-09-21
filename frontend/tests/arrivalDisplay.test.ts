import { getArrivalDisplay } from '@/services/arrivalDisplay'

const now = new Date('2026-09-21T10:00:00+08:00')

describe('getArrivalDisplay', () => {
  it('uses scheduled time when arrival is more than 60 minutes away', () => {
    const result = getArrivalDisplay({
      now,
      scheduledAt: new Date('2026-09-21T11:01:00+08:00'),
      estimatedAt: new Date('2026-09-21T11:02:00+08:00'),
      sourceUpdatedAt: now,
    })

    expect(result.mode).toBe('scheduled')
  })

  it('uses realtime minutes inside the 60-minute window', () => {
    const result = getArrivalDisplay({
      now,
      scheduledAt: new Date('2026-09-21T10:20:00+08:00'),
      estimatedAt: new Date('2026-09-21T10:18:20+08:00'),
      sourceUpdatedAt: new Date('2026-09-21T09:59:30+08:00'),
    })

    expect(result).toMatchObject({ label: '19 分鐘', mode: 'realtime', stale: false })
  })

  it('shows arriving soon when less than one minute remains', () => {
    const result = getArrivalDisplay({
      now,
      scheduledAt: new Date('2026-09-21T10:01:00+08:00'),
      estimatedAt: new Date('2026-09-21T10:00:30+08:00'),
      sourceUpdatedAt: now,
    })

    expect(result.label).toBe('即將進站')
  })

  it('falls back to scheduled time when realtime data is stale', () => {
    const result = getArrivalDisplay({
      now,
      scheduledAt: new Date('2026-09-21T10:20:00+08:00'),
      estimatedAt: new Date('2026-09-21T10:18:00+08:00'),
      sourceUpdatedAt: new Date('2026-09-21T09:57:00+08:00'),
    })

    expect(result).toMatchObject({ mode: 'scheduled', stale: true })
  })

  it('formats scheduled time in the selected city time zone', () => {
    const result = getArrivalDisplay({
      now: new Date('2026-09-21T00:00:00Z'),
      scheduledAt: new Date('2026-09-21T02:00:00Z'),
      timeZone: 'Asia/Taipei',
    })

    expect(result).toMatchObject({ label: '10:00', mode: 'scheduled' })
  })
})
