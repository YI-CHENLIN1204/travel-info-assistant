import { getBusDirectionLabel } from '@/services/transitDirection'
import type { TransitRoute } from '@/types/api'

const route: TransitRoute = {
  id: 'TPE214',
  nameZh: '214',
  nameEn: '214',
  originName: '中和',
  destinationName: '內湖',
  operators: ['中興巴士'],
  directions: [
    {
      direction: 0,
      headsign: '214',
      originName: '中和',
      destinationName: '內湖',
    },
    {
      direction: 1,
      headsign: '214',
      originName: '內湖',
      destinationName: '中和',
    },
  ],
}

describe('getBusDirectionLabel', () => {
  it('uses each direction destination before a duplicated headsign', () => {
    expect(getBusDirectionLabel(route, 0)).toBe('往 內湖')
    expect(getBusDirectionLabel(route, 1)).toBe('往 中和')
  })

  it('falls back to outbound and return labels when metadata is missing', () => {
    expect(getBusDirectionLabel({ ...route, directions: [] }, 0)).toBe('去程')
    expect(getBusDirectionLabel({ ...route, directions: [] }, 1)).toBe('返程')
  })
})
