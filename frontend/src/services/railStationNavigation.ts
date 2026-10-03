import type { RailStation, ServiceCapability, TransitDirection } from '@/types/api'

export interface RailLocality {
  name: string
  query: string
}

export interface RailRegion {
  id: string
  name: string
  localities: RailLocality[]
}

const taiwanRailRegions: RailRegion[] = [
  {
    id: 'north',
    name: '北台灣',
    localities: [
      '基隆市',
      '臺北市',
      '新北市',
      '桃園市',
      '新竹縣',
      '新竹市',
      '宜蘭縣',
    ].map(toLocality),
  },
  {
    id: 'central',
    name: '中台灣',
    localities: ['苗栗縣', '臺中市', '彰化縣', '南投縣', '雲林縣'].map(toLocality),
  },
  {
    id: 'south',
    name: '南台灣',
    localities: ['嘉義縣', '嘉義市', '臺南市', '高雄市', '屏東縣'].map(toLocality),
  },
  {
    id: 'east',
    name: '東台灣',
    localities: ['花蓮縣', '臺東縣'].map(toLocality),
  },
]

export function findIntegratedRailService(
  services: readonly ServiceCapability[],
): ServiceCapability | null {
  return (
    services.find(
      (service) => service.serviceKey === 'rail' && service.integrationStatus === 'integrated',
    ) ?? null
  )
}

export function getRailNavigationLabel(
  countryCode: string,
  displayName: string | null | undefined,
): string {
  if (countryCode.toUpperCase() === 'TW') return '台鐵查詢'

  const serviceName = displayName?.trim() || '鐵路'
  return serviceName.endsWith('查詢') ? serviceName : `${serviceName}查詢`
}

export function getRailRegions(countryCode: string): readonly RailRegion[] {
  return countryCode.toUpperCase() === 'TW' ? taiwanRailRegions : []
}

export function getRailDirectionLabel(
  countryCode: string,
  direction: TransitDirection,
): string {
  if (countryCode.toUpperCase() === 'TW') {
    if (direction.direction === 0) return '往南'
    if (direction.direction === 1) return '往北'
  }

  const destination = direction.destinationName ?? direction.headsign
  return destination ? `往 ${destination}` : `方向 ${direction.direction + 1}`
}

export function orderRailStations(
  stations: readonly RailStation[],
  direction: number | null,
): RailStation[] {
  const ordered = stations
    .map((station, originalIndex) => ({
      station,
      originalIndex,
      position: [...(station.linePositions ?? [])].sort(compareLinePositions)[0] ?? null,
    }))
    .sort((left, right) => {
      if (!left.position && !right.position) return left.originalIndex - right.originalIndex
      if (!left.position) return 1
      if (!right.position) return -1

      const lineOrder = left.position.lineId.localeCompare(right.position.lineId)
      if (lineOrder !== 0) return lineOrder

      const sequenceOrder = left.position.sequence - right.position.sequence
      return sequenceOrder || left.originalIndex - right.originalIndex
    })
    .map(({ station }) => station)

  return direction === 1 ? ordered.reverse() : ordered
}

function compareLinePositions(
  left: RailStation['linePositions'][number],
  right: RailStation['linePositions'][number],
): number {
  const lineOrder = left.lineId.localeCompare(right.lineId)
  return lineOrder || left.sequence - right.sequence
}

function toLocality(name: string): RailLocality {
  return { name, query: name }
}
