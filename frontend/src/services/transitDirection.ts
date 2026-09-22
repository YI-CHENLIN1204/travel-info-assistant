import type { TransitRoute } from '@/types/api'

export function getBusDirectionLabel(route: TransitRoute, direction: number): string {
  const item = route.directions.find((value) => value.direction === direction)

  // The route/sub-route headsign supplied by TDX is sometimes the route name for
  // both directions. The direction-specific destination is the useful label.
  if (item?.destinationName) return `往 ${item.destinationName}`
  if (item?.headsign) return `往 ${item.headsign}`
  return direction === 0 ? '去程' : '返程'
}
