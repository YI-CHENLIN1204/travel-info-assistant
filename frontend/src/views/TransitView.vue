<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import {
  BusFront,
  CircleAlert,
  Database,
  Gauge,
  MapPinned,
  RefreshCw,
  Search,
  TrainFront,
} from '@lucide/vue'
import StatusPill from '@/components/StatusPill.vue'
import {
  getArrivalDisplay,
  getTransitEmptyMessage,
  type ArrivalDisplayResult,
} from '@/services/arrivalDisplay'
import { getBusDirectionLabel } from '@/services/transitDirection'
import {
  getTaiwanTraditionalConverter,
  identityTextConverter,
  type TextConverter,
} from '@/services/traditionalChinese'
import { useCityStore } from '@/stores/city'
import {
  isMetroStatusCurrent,
  useTransitStore,
  type TransitModeKey,
} from '@/stores/transit'
import type {
  MetroStation,
  TransitArrival,
  TransitDirection,
  TransitRoute,
  TransitStop,
} from '@/types/api'

const cityStore = useCityStore()
const transitStore = useTransitStore()
const now = ref(new Date())
const textConverter = ref<TextConverter>(identityTextConverter)
let clockTimer: number | undefined

const integratedServices = computed(() =>
  cityStore.currentCity.services.filter(
    (service) =>
      service.integrationStatus === 'integrated' &&
      (service.serviceKey === 'bus' || service.serviceKey === 'metro'),
  ),
)

const availableModes = computed<TransitModeKey[]>(() =>
  integratedServices.value.map((service) => service.serviceKey as TransitModeKey),
)

const activeService = computed(() =>
  integratedServices.value.find((service) => service.serviceKey === transitStore.activeMode),
)
const busModeLabel = computed(
  () => integratedServices.value.find((service) => service.serviceKey === 'bus')?.displayName ?? '公車',
)
const metroModeLabel = computed(
  () => integratedServices.value.find((service) => service.serviceKey === 'metro')?.displayName ?? '捷運',
)

const isTokyo = computed(() => cityStore.currentCity.code === 'tokyo')
const isHongKong = computed(() => cityStore.currentCity.code === 'hong-kong')
const isSingapore = computed(() => cityStore.currentCity.code === 'singapore')
const isTaipei = computed(() => cityStore.currentCity.code === 'taipei')
const usesRouteMetro = computed(
  () => isTaipei.value || isTokyo.value || isHongKong.value || isSingapore.value,
)
const usesRealtimeMetro = computed(
  () =>
    isTaipei.value ||
    isHongKong.value ||
    isSingapore.value ||
    (isTokyo.value &&
      transitStore.selectedMetroRoute?.id.startsWith('odpt.Railway:Toei.') === true),
)
const metroSearchPlaceholder = computed(() => {
  if (isTaipei.value) return '例如：板南線、台北車站、BL12'
  if (isHongKong.value) return '例如：尖沙咀、Central、TST'
  if (isSingapore.value) return '例如：烏節、Orchard、NS22'
  return '例如：銀座、Asakusa、A18'
})
const providerConfigured = computed(() => transitStore.providerStatus?.configured === true)
const statusTone = computed<'ready' | 'warning' | 'neutral'>(() => {
  if (availableModes.value.length === 0) return 'neutral'
  if (isTokyo.value) {
    if (!transitStore.resultMeta || transitStore.resultMeta.source !== 'ODPT') return 'neutral'
    return transitStore.resultMeta.dataStatus === 'unavailable' ? 'warning' : 'ready'
  }
  if (isHongKong.value) {
    if (!transitStore.resultMeta) return 'neutral'
    return transitStore.resultMeta.dataStatus === 'unavailable' ? 'warning' : 'ready'
  }
  if (isSingapore.value) {
    if (!transitStore.resultMeta) return 'neutral'
    return transitStore.resultMeta.dataStatus === 'unavailable' ? 'warning' : 'ready'
  }
  return providerConfigured.value ? 'ready' : 'warning'
})
const statusLabel = computed(() => {
  if (availableModes.value.length === 0) return '此城市尚未整合'
  if (isTokyo.value) {
    if (!transitStore.resultMeta || transitStore.resultMeta.source !== 'ODPT') {
      return 'ODPT 連線確認中'
    }
    return transitStore.resultMeta.dataStatus === 'unavailable'
      ? 'ODPT 金鑰或服務待確認'
      : 'ODPT 官方資料已啟用'
  }
  if (isHongKong.value) {
    const label = transitStore.activeMode === 'bus' ? '巴士' : '港鐵'
    if (!transitStore.resultMeta) return `${label}資料連線確認中`
    return transitStore.resultMeta.dataStatus === 'unavailable'
      ? `${label}官方資料暫時無法使用`
      : `${label}官方資料已啟用`
  }
  if (isSingapore.value) {
    if (!transitStore.resultMeta) return 'LTA DataMall 連線確認中'
    if (transitStore.resultMeta.dataStatus === 'unavailable') {
      return 'LTA API Account Key 或服務待確認'
    }
    if (transitStore.resultMeta.dataStatus === 'scheduled') {
      return 'LTA 官方班表已啟用'
    }
    if (transitStore.resultMeta.dataStatus === 'cached') {
      return 'LTA 備援資料已啟用'
    }
    return 'LTA 官方即時預估已啟用'
  }
  return providerConfigured.value ? 'TDX 真實資料已啟用' : 'TDX 金鑰待設定'
})
const serviceSignature = computed(() =>
  cityStore.currentCity.services
    .map(
      (service) =>
        `${service.serviceKey}:${service.integrationStatus}:${service.availabilityStatus}`,
    )
    .join('|'),
)
const quotaPercent = computed(() => {
  const status = transitStore.providerStatus
  if (!status || status.softLimitPoints <= 0) return 0
  return Math.min(100, (status.estimatedPoints / status.softLimitPoints) * 100)
})
const selectedMetroStatus = computed(() => {
  const status = transitStore.selectedMetroStatus
  return status && isMetroStatusCurrent(status, now.value) ? status : null
})
const displayedRouteMetroStations = computed(() =>
  isTaipei.value && !transitStore.selectedMetroRoute
    ? []
    : transitStore.filteredMetroStations,
)
const selectedMetroStatusExpired = computed(
  () =>
    transitStore.selectedMetroStatus !== null &&
    selectedMetroStatus.value === null,
)

watch(
  [() => cityStore.currentCity.id, serviceSignature],
  async ([cityId]) => {
    transitStore.resetResults()
    await loadTextConverter(cityStore.currentCity.code)
    if (cityStore.currentCity.id !== cityId) return

    if (isTaipei.value) await transitStore.loadProviderStatus()
    const firstMode = availableModes.value[0]
    if (!firstMode) return

    if (!availableModes.value.includes(transitStore.activeMode)) {
      transitStore.activeMode = firstMode
    }
    await loadModeIndex(transitStore.activeMode)
  },
  { immediate: true },
)

watch(
  [
    usesRouteMetro,
    () => transitStore.activeMode,
    () => transitStore.selectedMetroRoute?.id,
    () => cityStore.currentCity.id,
  ],
  ([routeMetro, mode, routeId, cityId]) => {
    transitStore.stopMetroStatusRefresh()
    if (routeMetro && mode === 'metro' && routeId) {
      transitStore.startMetroStatusRefresh(cityId, routeId)
    }
  },
  { immediate: true },
)

watch(
  [
    () => transitStore.activeMode,
    () => transitStore.selectedStop?.id,
    () => cityStore.currentCity.id,
  ],
  ([mode, stopId, cityId]) => {
    transitStore.stopBusArrivalRefresh()
    if (mode === 'bus' && stopId) {
      transitStore.startBusArrivalRefresh(cityId)
    }
  },
  { immediate: true },
)

watch(
  [
    usesRealtimeMetro,
    () => transitStore.activeMode,
    () => transitStore.selectedStation?.id,
    () => cityStore.currentCity.id,
  ],
  ([realtimeMetro, mode, stationId, cityId]) => {
    transitStore.stopMetroArrivalRefresh()
    if (realtimeMetro && mode === 'metro' && stationId) {
      transitStore.startMetroArrivalRefresh(cityId)
    }
  },
  { immediate: true },
)

onMounted(() => {
  clockTimer = window.setInterval(() => {
    now.value = new Date()
  }, 15_000)
})

onUnmounted(() => {
  if (clockTimer !== undefined) window.clearInterval(clockTimer)
  transitStore.stopMetroStatusRefresh()
  transitStore.stopBusArrivalRefresh()
  transitStore.stopMetroArrivalRefresh()
})

async function activateMode(mode: TransitModeKey): Promise<void> {
  transitStore.activeMode = mode
  transitStore.arrivals = []
  transitStore.resultMeta = null
  transitStore.selectedRoute = null
  transitStore.selectedStop = null
  transitStore.stops = []
  transitStore.selectedStation = null
  transitStore.selectedMetroDirection = null
  transitStore.selectedRailStation = null
  transitStore.selectedRailHeading = null
  await loadModeIndex(mode)
}

async function loadModeIndex(mode: TransitModeKey): Promise<void> {
  const cityId = cityStore.currentCity.id
  if (mode === 'bus' && transitStore.routes.length === 0) {
    await transitStore.searchBusRoutes(cityId)
  }
  if (mode === 'metro' && transitStore.stations.length === 0) {
    if (usesRouteMetro.value) {
      await transitStore.searchRouteMetro(cityId)
    } else {
      await transitStore.searchMetroStations(cityId)
    }
  }
}

function submitBusSearch(): void {
  void transitStore.searchBusRoutes(cityStore.currentCity.id)
}

function selectRoute(route: TransitRoute): void {
  void transitStore.chooseBusRoute(cityStore.currentCity.id, route)
}

function selectDirection(direction: number): void {
  void transitStore.chooseBusDirection(cityStore.currentCity.id, direction)
}

function onStopChange(event: Event): void {
  const stopId = (event.target as HTMLSelectElement).value
  const stop = transitStore.stops.find((item) => item.id === stopId)
  if (stop) selectStop(stop)
}

function selectStop(stop: TransitStop): void {
  void transitStore.chooseBusStop(cityStore.currentCity.id, stop)
}

function submitMetroSearch(): void {
  if (usesRouteMetro.value) {
    void transitStore.searchRouteMetro(cityStore.currentCity.id)
  } else {
    void transitStore.searchMetroStations(cityStore.currentCity.id)
  }
}

function selectStation(station: MetroStation): void {
  void transitStore.chooseMetroStation(cityStore.currentCity.id, station)
}

function selectMetroRoute(route: TransitRoute): void {
  void transitStore.chooseMetroRoute(cityStore.currentCity.id, route)
}

function selectMetroDirection(direction: number): void {
  transitStore.chooseMetroDirection(direction)
}

function refreshMetroStatus(): void {
  void transitStore.refreshMetroStatus(
    cityStore.currentCity.id,
    transitStore.selectedMetroRoute?.id,
  )
}

function refreshArrivals(): void {
  if (transitStore.activeMode === 'bus') {
    void transitStore.refreshBusArrivals(cityStore.currentCity.id)
  } else if (transitStore.activeMode === 'metro') {
    void transitStore.refreshMetroArrivals(cityStore.currentCity.id)
  }
}

function getArrivalView(arrival: TransitArrival): ArrivalDisplayResult {
  const scheduledAt = arrival.scheduledAt ?? arrival.estimatedAt
  if (!scheduledAt) {
    return {
      label: arrival.serviceStatus,
      mode: 'scheduled',
      stale: false,
      scheduledLabel: '',
    }
  }

  return getArrivalDisplay({
    now: now.value,
    scheduledAt: new Date(scheduledAt),
    estimatedAt: arrival.estimatedAt ? new Date(arrival.estimatedAt) : null,
    sourceUpdatedAt: arrival.sourceUpdatedAt ? new Date(arrival.sourceUpdatedAt) : null,
    timeZone: cityStore.currentCity.timeZone,
  })
}

function arrivalTimingCaption(arrival: TransitArrival): string {
  const view = getArrivalView(arrival)
  const labels: string[] = []
  const hasDistinctScheduledTime =
    Boolean(arrival.scheduledAt) &&
    (!arrival.estimatedAt ||
      Date.parse(arrival.scheduledAt!) !== Date.parse(arrival.estimatedAt))
  if (arrival.isLastService) labels.push('末班車')

  if (view.mode === 'realtime') {
    labels.push('即時預估')
    const serviceStatus = localize(arrival.serviceStatus)
    if (
      serviceStatus &&
      serviceStatus !== '即時預估' &&
      serviceStatus !== '正常營運'
    ) {
      labels.push(serviceStatus)
    }
  } else if (view.stale) {
    labels.push('即時資料已逾時')
  }

  if (hasDistinctScheduledTime && view.scheduledLabel) {
    labels.push(
      view.label === view.scheduledLabel && view.mode === 'scheduled'
        ? '表定時間'
        : `表定 ${view.scheduledLabel}`,
    )
  } else if (view.mode === 'scheduled' && arrival.scheduledAt && !arrival.estimatedAt) {
    labels.push('表定時間')
  }

  return labels.join(' · ')
}

function metroEmptyMessage(fallback: string): string {
  return transitStore.error ?? getTransitEmptyMessage(
    transitStore.resultMeta,
    cityStore.currentCity.timeZone,
    fallback,
  )
}

function directionLabel(route: TransitRoute, direction: number): string {
  return localize(getBusDirectionLabel(route, direction))
}

function metroDirectionLabel(direction: TransitDirection): string {
  const destination = direction.destinationName ?? direction.headsign
  return destination ? `往 ${localize(destination)}` : `方向 ${direction.direction + 1}`
}

function localize(value: string | null | undefined): string {
  return textConverter.value(value ?? '')
}

async function loadTextConverter(cityCode: string): Promise<void> {
  textConverter.value = identityTextConverter
  const converter = await getTaiwanTraditionalConverter(cityCode)
  if (cityStore.currentCity.code === cityCode) {
    textConverter.value = converter
  }
}

function formatTimestamp(value: string | null | undefined): string {
  if (!value) return '尚無更新時間'
  return new Intl.DateTimeFormat('zh-TW', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: cityStore.currentCity.timeZone,
  }).format(new Date(value))
}
</script>

<template>
  <div class="content-stack">
    <section class="page-intro transit-intro">
      <div>
        <StatusPill :tone="statusTone" :label="statusLabel" />
        <h2>{{ cityStore.currentCity.nameZh }}大眾運輸</h2>
        <p>
          <template v-if="isTokyo">
            查詢 Tokyo Metro 與都營地下鐵路線、車站、方向及官方運行狀態；都營班次會在官方資料有效時以延誤秒數修正預估時間，其餘班次明確顯示表定時間。
          </template>
          <template v-else-if="isHongKong">
            查詢港鐵與巴士路線、車站／站牌及即時到站資訊；資料由後端統一向香港官方開放數據取得並共用快取。
          </template>
          <template v-else-if="isSingapore">
            查詢新加坡 MRT 路線、車站、即時預估、表定班次與官方服務警示；資料由後端統一向 LTA DataMall 取得並共用快取。
          </template>
          <template v-else>
            查詢公車與捷運班次；跨城市鐵路已移至獨立查詢工具，資料仍由後端統一取得並共用快取。
          </template>
        </p>
      </div>
      <div class="intro-icon"><MapPinned :size="34" /></div>
    </section>

    <section v-if="availableModes.length" class="transit-workspace">
      <div class="transit-mode-tabs" role="tablist" aria-label="交通工具">
        <button
          v-if="availableModes.includes('bus')"
          class="transit-mode-tab"
          :class="{ active: transitStore.activeMode === 'bus' }"
          type="button"
          role="tab"
          :aria-selected="transitStore.activeMode === 'bus'"
          @click="activateMode('bus')"
        >
          <BusFront :size="20" />
          <span>{{ busModeLabel }}</span>
        </button>
        <button
          v-if="availableModes.includes('metro')"
          class="transit-mode-tab"
          :class="{ active: transitStore.activeMode === 'metro' }"
          type="button"
          role="tab"
          :aria-selected="transitStore.activeMode === 'metro'"
          @click="activateMode('metro')"
        >
          <TrainFront :size="20" />
          <span>{{ metroModeLabel }}</span>
        </button>
      </div>

      <div
        v-if="activeService?.availabilityStatus === 'temporarilyUnavailable'"
        class="provider-message provider-message-warning"
      >
        <CircleAlert :size="19" />
        <span>{{ activeService.message ?? '資料來源暫時無法更新，入口仍會保留。' }}</span>
      </div>

      <div v-if="transitStore.error" class="provider-message provider-message-error">
        <CircleAlert :size="19" />
        <span>{{ transitStore.error }}</span>
      </div>

      <template v-if="transitStore.activeMode === 'bus'">
        <form class="transit-search-form" @submit.prevent="submitBusSearch">
          <label>
            <span>{{ busModeLabel }}路線、起訖站或營運業者</span>
            <span class="input-shell">
              <Search :size="18" />
              <input
                v-model="transitStore.busQuery"
                maxlength="50"
                autocomplete="off"
                placeholder="例如：307、台北車站"
              />
            </span>
          </label>
          <button class="button button-primary search-button" type="submit" :disabled="transitStore.loading">
            {{ transitStore.loading ? '查詢中' : '查詢路線' }}
          </button>
        </form>

        <div v-if="transitStore.routes.length" class="transit-results-layout">
          <section class="selection-panel">
            <div class="panel-heading-row">
              <div>
                <span class="eyebrow">BUS ROUTES</span>
                <h3>選擇路線</h3>
              </div>
              <span>{{ transitStore.routes.length }} 筆</span>
            </div>
            <div class="route-result-list">
              <button
                v-for="route in transitStore.routes"
                :key="route.id"
                class="route-result"
                :class="{ selected: transitStore.selectedRoute?.id === route.id }"
                type="button"
                @click="selectRoute(route)"
              >
                <strong>{{ route.nameZh }}</strong>
                <span>{{ route.originName ?? '起點待確認' }} → {{ route.destinationName ?? '終點待確認' }}</span>
                <small v-if="route.operators.length">{{ route.operators.join('、') }}</small>
              </button>
            </div>
          </section>

          <section class="arrival-panel">
            <template v-if="transitStore.selectedRoute">
              <div class="panel-heading-row arrival-heading">
                <div>
                  <span class="eyebrow">{{ transitStore.selectedRoute.nameZh }}</span>
                  <h3>站牌到站資訊</h3>
                </div>
                <button
                  v-if="transitStore.selectedStop"
                  class="icon-button refresh-button"
                  type="button"
                  aria-label="更新到站資訊"
                  :disabled="transitStore.loading"
                  @click="refreshArrivals"
                >
                  <RefreshCw :size="18" />
                </button>
              </div>

              <div class="direction-switch" :aria-label="`${busModeLabel}方向`">
                <button
                  v-for="direction in transitStore.selectedRoute.directions"
                  :key="direction.direction"
                  type="button"
                  :class="{ active: transitStore.selectedDirection === direction.direction }"
                  @click="selectDirection(direction.direction)"
                >
                  {{ directionLabel(transitStore.selectedRoute, direction.direction) }}
                </button>
              </div>

              <label class="stop-selector">
                <span>選擇站牌</span>
                <select :value="transitStore.selectedStop?.id ?? ''" @change="onStopChange">
                  <option value="" disabled>請選擇站牌</option>
                  <option v-for="stop in transitStore.stops" :key="stop.id" :value="stop.id">
                    {{ stop.sequence }}. {{ stop.nameZh }}
                  </option>
                </select>
              </label>

              <div v-if="transitStore.selectedStop" class="arrival-list">
                <article v-for="arrival in transitStore.arrivals" :key="arrival.id" class="arrival-card">
                  <div class="arrival-icon"><BusFront :size="20" /></div>
                  <div class="arrival-main">
                    <strong>{{ arrival.routeName ?? transitStore.selectedRoute.nameZh }}</strong>
                    <span>
                      {{
                        arrival.destinationName
                          ? `往 ${arrival.destinationName}`
                          : directionLabel(transitStore.selectedRoute, transitStore.selectedDirection)
                      }}
                    </span>
                  </div>
                  <div class="arrival-time">
                    <strong>{{ getArrivalView(arrival).label }}</strong>
                    <span :class="`data-mode-${getArrivalView(arrival).mode}`">
                      {{ arrivalTimingCaption(arrival) }}
                    </span>
                  </div>
                </article>
                <div v-if="!transitStore.arrivals.length && !transitStore.loading" class="inline-empty">
                  {{ getTransitEmptyMessage(
                    transitStore.resultMeta,
                    cityStore.currentCity.timeZone,
                    '目前查無這個站牌的到站資料。',
                  ) }}
                </div>
              </div>
              <div v-else class="inline-empty">選擇站牌後才會取得到站資訊。</div>
            </template>
            <div v-else class="inline-empty large">請先從左側選擇一條公車路線。</div>
          </section>
        </div>

        <div v-else-if="!transitStore.loading" class="inline-empty standalone">
          {{ transitStore.resultMeta?.message ?? `沒有符合條件的${busModeLabel}路線。` }}
        </div>
      </template>

      <template v-else-if="transitStore.activeMode === 'metro' && usesRouteMetro">
        <form class="transit-search-form" @submit.prevent="submitMetroSearch">
          <label>
            <span>{{ cityStore.currentCity.nameZh }}{{ metroModeLabel }}路線、車站名或車站代碼</span>
            <span class="input-shell">
              <Search :size="18" />
              <input
                v-model="transitStore.metroQuery"
                maxlength="50"
                autocomplete="off"
                :placeholder="metroSearchPlaceholder"
              />
            </span>
          </label>
          <button class="button button-primary search-button" type="submit" :disabled="transitStore.loading">
            {{ transitStore.loading ? '查詢中' : '查詢路線與車站' }}
          </button>
        </form>

        <div
          v-if="transitStore.metroRoutes.length || transitStore.stations.length"
          class="transit-results-layout"
        >
          <section class="selection-panel">
            <div class="panel-heading-row">
              <div>
                <span class="eyebrow">METRO LINES</span>
                <h3>路線</h3>
              </div>
              <span>{{ transitStore.metroRoutes.length }} 筆</span>
            </div>
            <div class="route-result-list">
              <article
                v-for="route in transitStore.metroRoutes"
                :key="route.id"
                class="route-result static-result"
                :class="{ selected: transitStore.selectedMetroRoute?.id === route.id }"
                role="button"
                tabindex="0"
                @click="selectMetroRoute(route)"
                @keydown.enter.prevent="selectMetroRoute(route)"
                @keydown.space.prevent="selectMetroRoute(route)"
              >
                <strong>{{ localize(route.nameZh) }}</strong>
                <span>{{ route.nameEn ?? `${cityStore.currentCity.nameEn} Metro` }}</span>
                <small>{{ route.operators.join('・') }}</small>
                <small>{{ localize(route.originName) || '起點待確認' }} → {{ localize(route.destinationName) || '終點待確認' }}</small>
                <small v-if="route.stationNames.length" class="route-stations">
                  {{ route.stationNames.map(localize).join(' · ') }}
                </small>
              </article>
              <div v-if="!transitStore.metroRoutes.length" class="inline-empty">
                沒有符合條件的路線。
              </div>
            </div>
          </section>

          <section class="arrival-panel">
            <div class="panel-heading-row">
              <div>
                <span class="eyebrow">METRO STATIONS</span>
                <h3>車站</h3>
              </div>
              <span>{{ displayedRouteMetroStations.length }} 筆</span>
            </div>

            <article
              v-if="transitStore.selectedMetroRoute"
              class="quota-panel panel-heading-row"
            >
              <div class="panel-icon">
                <TrainFront v-if="selectedMetroStatus" :size="21" />
                <CircleAlert v-else :size="21" />
              </div>
              <div class="panel-heading-row">
                <div>
                  <span class="eyebrow">OFFICIAL SERVICE STATUS</span>
                  <h3>{{ localize(transitStore.selectedMetroRoute.nameZh) }}</h3>
                </div>
                <button
                  class="icon-button refresh-button"
                  type="button"
                  :aria-label="`更新${cityStore.currentCity.nameZh}${metroModeLabel}運行狀態`"
                  :disabled="transitStore.metroStatusLoading"
                  @click="refreshMetroStatus"
                >
                  <RefreshCw :size="18" />
                </button>
              </div>
              <template v-if="selectedMetroStatus">
                <p>
                {{
                  localize(
                    selectedMetroStatus.messageZh ??
                    selectedMetroStatus.messageJa ??
                    selectedMetroStatus.messageEn ??
                    '暫時無法確認運行狀態。',
                  )
                }}
                <br />更新 {{ formatTimestamp(selectedMetroStatus.updatedAt) }} ·
                有效至 {{ formatTimestamp(selectedMetroStatus.validUntil) }}
                </p>
                <details
                  v-if="
                    selectedMetroStatus.messageZh &&
                    (selectedMetroStatus.messageJa || selectedMetroStatus.messageEn)
                  "
                  class="official-message-original"
                >
                  <summary>查看官方原文</summary>
                  <p v-if="selectedMetroStatus.messageJa" lang="ja">
                    {{ selectedMetroStatus.messageJa }}
                  </p>
                  <p v-if="selectedMetroStatus.messageEn" lang="en">
                    {{ selectedMetroStatus.messageEn }}
                  </p>
                </details>
              </template>
              <p v-else>
                {{
                  selectedMetroStatusExpired
                    ? '官方運行狀態已超過有效期限，正在重新確認。'
                    : transitStore.metroStatusMeta?.dataStatus === 'unavailable'
                    ? (transitStore.metroStatusMeta.message ?? '暫時無法取得官方運行狀態。')
                    : `目前沒有 ${localize(transitStore.selectedMetroRoute.nameZh)} 可顯示的官方運行狀態。`
                }}
              </p>
            </article>

            <div
              v-if="transitStore.selectedMetroRoute && transitStore.metroDirectionOptions.length"
              class="direction-switch metro-direction-switch"
              :aria-label="`${metroModeLabel}方向`"
            >
              <button
                v-for="direction in transitStore.metroDirectionOptions"
                :key="direction.direction"
                type="button"
                :class="{ active: transitStore.selectedMetroDirection === direction.direction }"
                @click="selectMetroDirection(direction.direction)"
              >
                {{ metroDirectionLabel(direction) }}
              </button>
            </div>

            <div class="route-result-list route-metro-station-list">
              <button
                v-for="station in displayedRouteMetroStations"
                :key="station.id"
                class="route-result"
                :class="{ selected: transitStore.selectedStation?.id === station.id }"
                type="button"
                @click="selectStation(station)"
              >
                <strong>{{ localize(station.nameZh) }}</strong>
                <span>{{ station.nameEn ?? station.railwayName ?? `${cityStore.currentCity.nameEn} Metro` }}</span>
                <small>
                  {{ station.code ?? '代碼待確認' }}
                  <template v-if="station.railwayName"> · {{ localize(station.railwayName) }}</template>
                </small>
              </button>
              <div v-if="!displayedRouteMetroStations.length" class="inline-empty">
                  {{ transitStore.selectedMetroRoute ? '此路線沒有可顯示的車站。' : `請先選擇一條${metroModeLabel}路線。` }}
              </div>
            </div>

            <div v-if="transitStore.selectedStation" class="route-metro-timetable">
              <div class="panel-heading-row arrival-heading">
                <div>
                  <span class="eyebrow">
                    {{ transitStore.selectedStation.code ?? transitStore.selectedStation.id }}
                  </span>
                  <h3>{{ localize(transitStore.selectedStation.nameZh) }}接下來班次</h3>
                </div>
                <button
                  class="icon-button refresh-button"
                  type="button"
                  :aria-label="`更新${cityStore.currentCity.nameZh}${metroModeLabel}班次`"
                  :disabled="transitStore.loading"
                  @click="refreshArrivals"
                >
                  <RefreshCw :size="18" />
                </button>
              </div>

              <div class="arrival-list metro-arrivals">
                <article v-for="arrival in transitStore.visibleMetroArrivals" :key="arrival.id" class="arrival-card">
                  <div class="arrival-icon"><TrainFront :size="20" /></div>
                  <div class="arrival-main">
                    <strong>{{ localize(arrival.lineName) || `${cityStore.currentCity.nameEn} Metro` }}</strong>
                    <span>
                      {{ arrival.destinationName ? `往 ${localize(arrival.destinationName)}` : localize(arrival.serviceStatus) }}
                      {{ arrival.platform ? ` · ${arrival.platform} 月台` : '' }}
                    </span>
                  </div>
                  <div class="arrival-time">
                    <strong>{{ getArrivalView(arrival).label }}</strong>
                    <span :class="`data-mode-${getArrivalView(arrival).mode}`">
                      {{ arrivalTimingCaption(arrival) }}
                    </span>
                  </div>
                </article>
                <div v-if="!transitStore.visibleMetroArrivals.length && !transitStore.loading" class="inline-empty">
                  {{ metroEmptyMessage(
                    usesRealtimeMetro
                      ? '目前查無接下來的即時到站班次。'
                      : '目前查無接下來的表定班次。',
                  ) }}
                </div>
              </div>
            </div>
            <div v-else-if="transitStore.stations.length" class="inline-empty route-metro-station-prompt">
              {{ usesRealtimeMetro ? '選擇車站後查看即時到站班次。' : '選擇車站後查看當日表定班次。' }}
            </div>
          </section>
        </div>

        <div v-else-if="!transitStore.loading" class="inline-empty standalone">
          {{ transitStore.resultMeta?.message ?? `沒有符合條件的${cityStore.currentCity.nameZh}${metroModeLabel}路線或車站。` }}
        </div>
      </template>

      <template v-else-if="transitStore.activeMode === 'metro'">
        <form class="transit-search-form" @submit.prevent="submitMetroSearch">
          <label>
            <span>{{ metroModeLabel }}站名或車站代碼</span>
            <span class="input-shell">
              <Search :size="18" />
              <input
                v-model="transitStore.metroQuery"
                maxlength="50"
                autocomplete="off"
                placeholder="例如：台北車站、BL12"
              />
            </span>
          </label>
          <button class="button button-primary search-button" type="submit" :disabled="transitStore.loading">
            {{ transitStore.loading ? '查詢中' : '查詢車站' }}
          </button>
        </form>

        <div v-if="transitStore.stations.length" class="transit-results-layout">
          <section class="selection-panel">
            <div class="panel-heading-row">
              <div>
                <span class="eyebrow">METRO STATIONS</span>
                <h3>選擇車站</h3>
              </div>
              <span>{{ transitStore.stations.length }} 筆</span>
            </div>
            <div class="route-result-list">
              <button
                v-for="station in transitStore.stations"
                :key="station.id"
                class="route-result"
                :class="{ selected: transitStore.selectedStation?.id === station.id }"
                type="button"
                @click="selectStation(station)"
              >
                <strong>{{ localize(station.nameZh) }}</strong>
                <span>{{ station.nameEn ?? station.address ?? metroModeLabel }}</span>
                <small>{{ station.id }}</small>
              </button>
            </div>
          </section>

          <section class="arrival-panel">
            <template v-if="transitStore.selectedStation">
              <div class="panel-heading-row arrival-heading">
                <div>
                  <span class="eyebrow">{{ transitStore.selectedStation.id }}</span>
                  <h3>{{ localize(transitStore.selectedStation.nameZh) }}列車資訊</h3>
                </div>
                <button
                  class="icon-button refresh-button"
                  type="button"
                  aria-label="更新列車資訊"
                  :disabled="transitStore.loading"
                  @click="refreshArrivals"
                >
                  <RefreshCw :size="18" />
                </button>
              </div>

              <div
                v-if="transitStore.metroDirectionOptions.length"
                class="direction-switch metro-direction-switch"
                :aria-label="`${metroModeLabel}方向`"
              >
                <button
                  v-for="direction in transitStore.metroDirectionOptions"
                  :key="direction.direction"
                  type="button"
                  :class="{ active: transitStore.selectedMetroDirection === direction.direction }"
                  @click="selectMetroDirection(direction.direction)"
                >
                  {{ metroDirectionLabel(direction) }}
                </button>
              </div>

              <div class="arrival-list metro-arrivals">
                <article v-for="arrival in transitStore.visibleMetroArrivals" :key="arrival.id" class="arrival-card">
                  <div class="arrival-icon"><TrainFront :size="20" /></div>
                  <div class="arrival-main">
                    <strong>{{ localize(arrival.lineName) || arrival.lineId || metroModeLabel }}</strong>
                    <span>{{ arrival.destinationName ? `往 ${localize(arrival.destinationName)}` : localize(arrival.serviceStatus) }}</span>
                  </div>
                  <div class="arrival-time">
                    <strong>{{ getArrivalView(arrival).label }}</strong>
                    <span :class="`data-mode-${getArrivalView(arrival).mode}`">
                      {{ arrivalTimingCaption(arrival) }}
                    </span>
                  </div>
                </article>
                <div v-if="!transitStore.visibleMetroArrivals.length && !transitStore.loading" class="inline-empty">
                  {{ metroEmptyMessage('目前查無這個車站的列車資料。') }}
                </div>
              </div>
            </template>
            <div v-else class="inline-empty large">請先從左側選擇一座{{ metroModeLabel }}站。</div>
          </section>
        </div>

        <div v-else-if="!transitStore.loading" class="inline-empty standalone">
          {{ transitStore.resultMeta?.message ?? `沒有符合條件的${metroModeLabel}站。` }}
        </div>
      </template>


      <footer v-if="transitStore.resultMeta" class="data-source-footer">
        <Database :size="15" />
        <span>
          資料來源 {{ transitStore.resultMeta.source }} ·
          更新 {{ formatTimestamp(transitStore.resultMeta.sourceUpdatedAt) }}
        </span>
        <span v-if="transitStore.resultMeta.stale" class="stale-badge">快取備援</span>
        <span v-if="transitStore.resultMeta.message">{{ transitStore.resultMeta.message }}</span>
        <span v-if="isTokyo">
          都營地下鐵資料：東京都交通局・公共交通オープンデータ協議会
        </span>
        <span v-else-if="isHongKong">港鐵資料：香港鐵路有限公司・DATA.GOV.HK</span>
        <span v-else-if="isSingapore">MRT 資料：新加坡陸路交通管理局・LTA DataMall</span>
      </footer>
    </section>

    <section v-else class="empty-state">
      <span class="empty-state-icon"><BusFront :size="34" /></span>
      <h3>目前尚無已整合的市區交通入口</h3>
      <p>這不代表{{ cityStore.currentCity.nameZh }}沒有公車或捷運，而是目前版本尚未完成相關資料源串接。</p>
    </section>

    <section v-if="isTaipei" class="transit-insights">
      <article class="quota-panel">
        <div class="panel-icon"><Gauge :size="21" /></div>
        <div>
          <span class="eyebrow">FREE QUOTA GUARD</span>
          <h3>TDX 免費額度保護</h3>
        </div>
        <template v-if="transitStore.providerStatus">
          <div class="quota-numbers">
            <strong>{{ transitStore.providerStatus.estimatedPoints.toFixed(4) }}</strong>
            <span>/ {{ transitStore.providerStatus.softLimitPoints.toFixed(1) }} 點內部停止線</span>
          </div>
          <div class="quota-track" aria-label="本月 TDX 估算使用率">
            <span :style="{ width: `${quotaPercent}%` }" />
          </div>
          <p>
            {{ transitStore.providerStatus.billingCycle }} 已記錄
            {{ transitStore.providerStatus.requestCount }} 次外部請求；每分鐘最多
            {{ transitStore.providerStatus.requestsPerMinute }} 次，付費超額已關閉。方案查核：
            {{ transitStore.providerStatus.pricingVerifiedAt }}。
          </p>
        </template>
        <p v-else>後端未連線，暫時無法讀取用量估算。</p>
      </article>

      <article class="rule-panel compact-rule-panel">
        <div>
          <span class="eyebrow">ARRIVAL RULE</span>
          <h3>到站顯示規則</h3>
        </div>
        <ul>
          <li><strong>60 分鐘以上</strong><span>顯示表定時間</span></li>
          <li><strong>60 分鐘內</strong><span>顯示分鐘倒數，並保留表定時間</span></li>
          <li><strong>少於 1 分鐘</strong><span>顯示「即將進站」</span></li>
          <li><strong>即時資料過期</strong><span>改用表定倒數並明確標示</span></li>
        </ul>
      </article>
    </section>

  </div>
</template>
