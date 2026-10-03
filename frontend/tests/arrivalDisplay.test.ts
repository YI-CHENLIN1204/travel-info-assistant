import { getArrivalDisplay, getTransitEmptyMessage } from '@/services/arrivalDisplay'
import type { ApiMeta } from '@/types/api'

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

    expect(result).toMatchObject({
      label: '20 分鐘',
      mode: 'scheduled',
      stale: true,
      scheduledLabel: '10:20',
    })
  })

  it('shows a local countdown while preserving the scheduled clock time', () => {
    const result = getArrivalDisplay({
      now,
      scheduledAt: new Date('2026-09-21T10:03:00+08:00'),
      timeZone: 'Asia/Taipei',
    })

    expect(result).toMatchObject({
      label: '3 分鐘',
      mode: 'scheduled',
      stale: false,
      scheduledLabel: '10:03',
    })
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

describe('getTransitEmptyMessage', () => {
  const baseMeta: ApiMeta = {
    dataStatus: 'scheduled',
    source: 'TDX',
    sourceUpdatedAt: null,
    fetchedAt: '2026-10-04T15:00:00Z',
    stale: false,
    message: null,
  }

  it('formats the scheduled last departure in the selected city time zone', () => {
    expect(
      getTransitEmptyMessage(
        {
          ...baseMeta,
          serviceDayStatus: 'ended',
          lastDepartureAt: '2026-10-04T15:48:00Z',
        },
        'Asia/Taipei',
        '目前查無班次。',
      ),
    ).toBe('本日已無車次，末班車已於 23:48 駛離站。')
  })

  it('does not claim service ended without an explicit valid last departure', () => {
    expect(
      getTransitEmptyMessage(
        { ...baseMeta, message: '資料來源暫時無法使用。' },
        'Asia/Taipei',
        '目前查無班次。',
      ),
    ).toBe('資料來源暫時無法使用。')
    expect(
      getTransitEmptyMessage(
        { ...baseMeta, serviceDayStatus: 'ended', lastDepartureAt: 'invalid' },
        'Asia/Taipei',
        '目前查無班次。',
      ),
    ).toBe('目前查無班次。')
  })
})
