export type TextConverter = (value: string) => string

export const identityTextConverter: TextConverter = (value) => value

const converters = new Map<string, Promise<TextConverter>>()

export function getTaiwanTraditionalConverter(cityCode: string): Promise<TextConverter> {
  if (!['singapore', 'hong-kong', 'tokyo'].includes(cityCode)) {
    return Promise.resolve(identityTextConverter)
  }

  const existing = converters.get(cityCode)
  if (existing) return existing

  const converter = createConverter(cityCode)
  converters.set(cityCode, converter)
  return converter
}

async function createConverter(cityCode: string): Promise<TextConverter> {
  const { ConverterFactory } = await import('opencc-js/core')

  if (cityCode === 'singapore') {
    const [{ default: fromCn }, { default: toTwp }] = await Promise.all([
      import('opencc-js/from/cn'),
      import('opencc-js/to/twp'),
    ])
    return ConverterFactory(fromCn, toTwp)
  }

  if (cityCode === 'hong-kong') {
    const [{ default: fromHk }, { default: toTw }] = await Promise.all([
      import('opencc-js/from/hk'),
      import('opencc-js/to/tw'),
    ])
    return ConverterFactory(fromHk, toTw)
  }

  const [{ default: fromJp }, { default: toTw }] = await Promise.all([
    import('opencc-js/from/jp'),
    import('opencc-js/to/tw'),
  ])
  return ConverterFactory(fromJp, toTw)
}
