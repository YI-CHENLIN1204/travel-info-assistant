export type ArrivalDisplayMode = 'realtime' | 'scheduled'

export interface ArrivalDisplayInput {
  scheduledAt: Date
  estimatedAt?: Date | null
  sourceUpdatedAt?: Date | null
  now: Date
}

export interface ArrivalDisplayResult {
  label: string
  mode: ArrivalDisplayMode
  stale: boolean
}

const realtimeWindowMinutes = 60
const staleAfterMinutes = 2

export function getArrivalDisplay(input: ArrivalDisplayInput): ArrivalDisplayResult {
  const scheduledMinutes = minutesBetween(input.now, input.scheduledAt)
  const sourceAgeMinutes = input.sourceUpdatedAt
    ? minutesBetween(input.sourceUpdatedAt, input.now)
    : Number.POSITIVE_INFINITY
  const realtimeFresh = sourceAgeMinutes <= staleAfterMinutes

  if (scheduledMinutes > realtimeWindowMinutes || !input.estimatedAt || !realtimeFresh) {
    return {
      label: formatTime(input.scheduledAt),
      mode: 'scheduled',
      stale: Boolean(input.estimatedAt) && !realtimeFresh,
    }
  }

  const estimatedMinutes = minutesBetween(input.now, input.estimatedAt)
  if (estimatedMinutes < 1) {
    return { label: '即將進站', mode: 'realtime', stale: false }
  }

  return {
    label: `${Math.ceil(estimatedMinutes)} 分鐘`,
    mode: 'realtime',
    stale: false,
  }
}

function minutesBetween(from: Date, to: Date): number {
  return (to.getTime() - from.getTime()) / 60_000
}

function formatTime(value: Date): string {
  return new Intl.DateTimeFormat('zh-TW', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(value)
}
