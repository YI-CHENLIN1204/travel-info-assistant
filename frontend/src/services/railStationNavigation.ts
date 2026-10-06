import type {
  RailHeading,
  RailServiceKey,
  RailStation,
  ServiceCapability,
} from '@/types/api'

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

const taiwanHighSpeedRailRegions: RailRegion[] = [
  {
    id: 'north',
    name: '北台灣',
    localities: ['臺北市', '新北市', '桃園市', '新竹縣'].map(toLocality),
  },
  {
    id: 'central',
    name: '中台灣',
    localities: ['苗栗縣', '臺中市', '彰化縣', '雲林縣'].map(toLocality),
  },
  {
    id: 'south',
    name: '南台灣',
    localities: ['嘉義縣', '臺南市', '高雄市'].map(toLocality),
  },
]

export function findIntegratedRailService(
  services: readonly ServiceCapability[],
  serviceKey: RailServiceKey = 'rail',
): ServiceCapability | null {
  return (
    services.find(
      (service) => service.serviceKey === serviceKey && service.integrationStatus === 'integrated',
    ) ?? null
  )
}

export function getRailNavigationLabel(
  countryCode: string,
  displayName: string | null | undefined,
  serviceKey: RailServiceKey = 'rail',
): string {
  if (serviceKey === 'high-speed-rail') return '高鐵查詢'
  if (countryCode.toUpperCase() === 'TW') return '台鐵查詢'

  const serviceName = displayName?.trim() || '鐵路'
  return serviceName.endsWith('查詢') ? serviceName : `${serviceName}查詢`
}

export function getRailRegions(
  countryCode: string,
  serviceKey: RailServiceKey = 'rail',
): readonly RailRegion[] {
  if (countryCode.toUpperCase() !== 'TW') return []
  return serviceKey === 'high-speed-rail'
    ? taiwanHighSpeedRailRegions
    : taiwanRailRegions
}

export function getRailHeadingLabel(heading: RailHeading): string {
  return heading === 'north' ? '往北' : '往南'
}

export function orderRailStations(
  stations: readonly RailStation[],
  heading: RailHeading | null,
): RailStation[] {
  const entries = stations
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

  if (!heading) return entries.map(({ station }) => station)

  const result: RailStation[] = []
  for (let start = 0; start < entries.length; ) {
    const lineId = entries[start]!.position?.lineId ?? null
    let end = start + 1
    while (end < entries.length && (entries[end]!.position?.lineId ?? null) === lineId) end += 1

    const group = entries.slice(start, end)
    const positioned = group.filter(({ station }) => station.latitude !== null)
    const firstLatitude = positioned[0]?.station.latitude
    const lastLatitude = positioned[positioned.length - 1]?.station.latitude
    const sequenceMovesNorth =
      firstLatitude !== null &&
      firstLatitude !== undefined &&
      lastLatitude !== null &&
      lastLatitude !== undefined &&
      lastLatitude > firstLatitude
    const shouldReverse =
      lineId !== null &&
      firstLatitude !== lastLatitude &&
      ((heading === 'north' && !sequenceMovesNorth) ||
        (heading === 'south' && sequenceMovesNorth))

    result.push(...(shouldReverse ? group.reverse() : group).map(({ station }) => station))
    start = end
  }
  return result
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
