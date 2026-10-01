import { getTaiwanTraditionalConverter } from '@/services/traditionalChinese'

describe('toTaiwanTraditional', () => {
  it('converts Singapore simplified Chinese into Taiwan traditional Chinese', async () => {
    const convert = await getTaiwanTraditionalConverter('singapore')

    expect(convert('环线与武吉班让轻轨线')).toBe('環線與武吉班讓輕軌線')
    expect(convert('列车服务正常')).toBe('列車服務正常')
  })

  it('normalizes Japanese shinjitai used in Tokyo station names', async () => {
    const convert = await getTaiwanTraditionalConverter('tokyo')

    expect(convert('浅草線・目黒')).toBe('淺草線・目黑')
  })

  it('leaves Taiwan text and non-Chinese text intact', async () => {
    const taipei = await getTaiwanTraditionalConverter('taipei')
    const singapore = await getTaiwanTraditionalConverter('singapore')

    expect(taipei('台北捷運')).toBe('台北捷運')
    expect(singapore('Circle Line')).toBe('Circle Line')
  })
})
