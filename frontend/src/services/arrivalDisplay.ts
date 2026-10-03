import type { ApiMeta } from '@/types/api'

export type ArrivalDisplayMode = 'realtime' | 'scheduled'

export interface ArrivalDisplayInput {
  scheduledAt: Date
  estimatedAt?: Date | null
  sourceUpdatedAt?: Date | null
  now: Date
  timeZone?: string
}

export interface ArrivalDisplayResult {
  label: string
  mode: ArrivalDisplayMode
  stale: boolean
  scheduledLabel: string
}

const realtimeWindowMinutes = 60
const staleAfterMinutes = 2

export function getArrivalDisplay(input: ArrivalDisplayInput): ArrivalDisplayResult {
  const scheduledMinutes = minutesBetween(input.now, input.scheduledAt)
  const scheduledLabel = formatTime(input.scheduledAt, input.timeZone)
  const sourceAgeMinutes = input.sourceUpdatedAt
    ? minutesBetween(input.sourceUpdatedAt, input.now)
    : Number.POSITIVE_INFINITY
  const realtimeFresh = sourceAgeMinutes <= staleAfterMinutes

  if (scheduledMinutes > realtimeWindowMinutes || !input.estimatedAt || !realtimeFresh) {
    if (scheduledMinutes <= realtimeWindowMinutes) {
      return {
        label: countdownLabel(scheduledMinutes),
        mode: 'scheduled',
        stale: Boolean(input.estimatedAt) && !realtimeFresh,
        scheduledLabel,
      }
    }

    return {
      label: scheduledLabel,
      mode: 'scheduled',
      stale: Boolean(input.estimatedAt) && !realtimeFresh,
      scheduledLabel,
    }
  }

  const estimatedMinutes = minutesBetween(input.now, input.estimatedAt)
  return {
    label: countdownLabel(estimatedMinutes),
    mode: 'realtime',
    stale: false,
    scheduledLabel,
  }
}

export function getTransitEmptyMessage(
  meta: ApiMeta | null,
  timeZone: string,
  fallback: string,
): string {
  if (meta?.serviceDayStatus === 'ended' && meta.lastDepartureAt) {
    const lastDepartureAt = new Date(meta.lastDepartureAt)
    if (!Number.isNaN(lastDepartureAt.getTime())) {
      return `本日已無車次，末班車已於 ${formatTime(lastDepartureAt, timeZone)} 駛離站。`
    }
  }

  return meta?.message ?? fallback
}

function countdownLabel(minutes: number): string {
  return minutes < 1 ? '即將進站' : `${Math.ceil(minutes)} 分鐘`
}

function minutesBetween(from: Date, to: Date): number {
  return (to.getTime() - from.getTime()) / 60_000
}

function formatTime(value: Date, timeZone?: string): string {
  return new Intl.DateTimeFormat('zh-TW', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone,
  }).format(value)
}
