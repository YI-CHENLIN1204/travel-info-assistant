import liff from '@line/liff'

export interface LiffState {
  enabled: boolean
  inClient: boolean
  initialized: boolean
}

export async function initializeLiff(): Promise<LiffState> {
  const liffId = import.meta.env.VITE_LIFF_ID?.trim()
  if (!liffId) {
    return { enabled: false, inClient: false, initialized: false }
  }

  await liff.init({ liffId })
  return {
    enabled: true,
    inClient: liff.isInClient(),
    initialized: true,
  }
}
