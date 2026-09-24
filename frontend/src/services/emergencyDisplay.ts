const categoryLabels: Record<string, string> = {
  police: '警察',
  fireMedical: '消防與救護',
  visitorSupport: '旅客服務',
  policeInformation: '警察一般資訊',
  mofaEmergency: '台灣外交部',
  mobileFallback: '行動電話備援',
}

export function getEmergencyCategoryLabel(category: string): string {
  return categoryLabels[category] ?? '緊急聯絡'
}

export function getEmergencyCategoryTone(category: string): string {
  if (category === 'police') return 'navy'
  if (category === 'fireMedical') return 'coral'
  if (category === 'visitorSupport') return 'teal'
  if (category === 'mofaEmergency') return 'violet'
  return 'slate'
}

export function formatVerifiedDate(value: string): string {
  const [year, month, day] = value.split('-')
  return year && month && day ? `${year}/${month}/${day}` : value
}
