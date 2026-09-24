export type AlertTone = 'gray' | 'yellow' | 'orange' | 'red'

export function getAlertTone(level: number): AlertTone {
  if (level >= 4) return 'red'
  if (level === 3) return 'orange'
  if (level === 2) return 'yellow'
  return 'gray'
}

export function getAlertAction(level: number): string {
  if (level >= 4) return '儘速離境'
  if (level === 3) return '避免前往'
  if (level === 2) return '特別注意安全'
  return '留意一般安全'
}
