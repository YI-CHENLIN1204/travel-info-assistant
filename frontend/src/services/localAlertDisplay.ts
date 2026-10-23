import type { LocalAlert, LocalAlertKind, LocalAlertSeverity } from '@/types/api'

// Product policy: show actionable travel hazards, not every local bulletin.
export function getTravelerAlerts(alerts: readonly LocalAlert[]): LocalAlert[] {
  const latest = new Map<string, LocalAlert>()
  for (const alert of alerts) {
    const category = `${alert.category} ${alert.title}`
    if (/水庫|放流|降雨|豪雨|暴雨|強風|道路封閉/.test(category)) continue
    const majorEvent = /颱風|熱帶氣旋|地震|海嘯|火山|噴火|疏散|撤離|避難/.test(category)
    const disruption = alert.kind === 'transport' && /停駛|停航|取消|中斷|封站/.test(category + alert.summary)
    const severe = alert.severity === 'severe' || alert.severity === 'emergency'
    if (!majorEvent && !disruption && !severe) continue
    const key = `${alert.sourceName}|${alert.category}|${alert.regionName}`
    const previous = latest.get(key)
    if (!previous || Date.parse(alert.updatedAt ?? '') > Date.parse(previous.updatedAt ?? '')) {
      latest.set(key, alert)
    }
  }
  const rank: Record<LocalAlertSeverity, number> = { unknown: 0, info: 0, advisory: 1, warning: 2, severe: 3, emergency: 4 }
  return [...latest.values()].sort((a, b) =>
    rank[b.severity] - rank[a.severity] || Date.parse(b.updatedAt ?? '') - Date.parse(a.updatedAt ?? ''))
}

export type LocalAlertTone = 'gray' | 'yellow' | 'orange' | 'red'

export function getTravelerAlertSummary(alert: LocalAlert): string {
  let text = alert.summary
  if (/颱風/.test(alert.category)) {
    const readable = text.search(/(?:輕度|中度|強烈)颱風\s/)
    if (readable >= 0) text = text.slice(readable)
  }
  return text.length > 240 ? `${text.slice(0, 240)}…` : text
}

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
