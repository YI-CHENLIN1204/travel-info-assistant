import type { LocalAlertKind, LocalAlertSeverity } from '@/types/api'

export type LocalAlertTone = 'gray' | 'yellow' | 'orange' | 'red'

const kindLabels: Record<LocalAlertKind, string> = {
  weather: '天候警報',
  naturalHazard: '天然災害',
  transport: '交通異常',
  infrastructure: '民生設施',
  health: '健康與環境',
  publicSafety: '公共安全',
  other: '其他警報',
}

export function getLocalAlertTone(severity: LocalAlertSeverity): LocalAlertTone {
  if (severity === 'emergency' || severity === 'severe') return 'red'
  if (severity === 'warning') return 'orange'
  if (severity === 'advisory') return 'yellow'
  return 'gray'
}

export function getLocalAlertKindLabel(kind: LocalAlertKind): string {
  return kindLabels[kind]
}
