<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { Database, MapPinned, RefreshCw, TrainFront } from '@lucide/vue'
import StatusPill from '@/components/StatusPill.vue'
import { getArrivalDisplay, type ArrivalDisplayResult } from '@/services/arrivalDisplay'
import {
  findIntegratedRailService,
  getRailDirectionLabel,
  getRailDirectionHeading,
  getRailNavigationLabel,
  getRailRegions,
  orderRailStations,
  type RailLocality,
} from '@/services/railStationNavigation'
import { useCityStore } from '@/stores/city'
import { useTransitStore } from '@/stores/transit'
import type { RailStation, TransitArrival, TransitDirection } from '@/types/api'

const cityStore = useCityStore()
const transitStore = useTransitStore()
const now = ref(new Date())
const selectedRegionId = ref<string | null>(null)
const selectedLocality = ref<RailLocality | null>(null)
let clockTimer: number | undefined

const railService = computed(() => findIntegratedRailService(cityStore.currentCity.services))
const railRegions = computed(() => getRailRegions(cityStore.currentCity.countryCode))
const selectedRegion = computed(
  () => railRegions.value.find((region) => region.id === selectedRegionId.value) ?? null,
)
const pageLabel = computed(() =>
  getRailNavigationLabel(cityStore.currentCity.countryCode, railService.value?.displayName),
)
const selectedRailHeading = computed(() =>
  getRailDirectionHeading(transitStore.arrivals, transitStore.selectedRailDirection),
)
const orderedRailStations = computed(() =>
  orderRailStations(transitStore.railStations, selectedRailHeading.value),
)
const isTaiwanRail = computed(() => cityStore.currentCity.countryCode === 'TW')
const statusTone = computed<'ready' | 'warning' | 'neutral'>(() => {
  const providerStatus = transitStore.providerStatus
  if (!railService.value) return 'neutral'
  if (railService.value.availabilityStatus === 'temporarilyUnavailable') return 'warning'
  if (isTaiwanRail.value) {
    if (!providerStatus) return 'neutral'
    if (!providerStatus.configured) return 'warning'
  }
  return 'ready'
})
const statusLabel = computed(() => {
  const providerStatus = transitStore.providerStatus
  if (!railService.value) return '目前城市尚未整合鐵路'
  if (railService.value.availabilityStatus === 'temporarilyUnavailable') {
    return railService.value.message ?? '鐵路資料暫時無法更新'
  }
  if (isTaiwanRail.value) {
    if (!providerStatus) return 'TDX 連線確認中'
    return providerStatus.configured ? '台鐵資料已啟用' : 'TDX 金鑰待設定'
  }
  return `${railService.value.displayName}資料已啟用`
})

watch(
  () => cityStore.currentCity.id,
  () => {
    selectedRegionId.value = null
    selectedLocality.value = null
    clearRailResults()
    if (isTaiwanRail.value && railService.value) void transitStore.loadProviderStatus()
  },
  { immediate: true },
)

watch(
  [() => transitStore.selectedRailStation?.id, () => cityStore.currentCity.id],
  ([stationId, cityId]) => {
    transitStore.stopRailArrivalRefresh()
    if (stationId) transitStore.startRailArrivalRefresh(cityId)
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
  transitStore.stopRailArrivalRefresh()
})

function chooseRegion(regionId: string): void {
  selectedRegionId.value = regionId
  selectedLocality.value = null
  clearRailResults()
}

async function chooseLocality(locality: RailLocality): Promise<void> {
  selectedLocality.value = locality
  clearRailResults()
  transitStore.railQuery = locality.query
  await transitStore.searchRailStations(cityStore.currentCity.id)
}

function chooseStation(station: RailStation): void {
  void transitStore.chooseRailStation(cityStore.currentCity.id, station)
}

function chooseDirection(direction: number): void {
  transitStore.chooseRailDirection(direction)
}

function clearRailResults(): void {
  transitStore.stopRailArrivalRefresh()
  transitStore.railQuery = ''
  transitStore.railStations = []
  transitStore.selectedRailStation = null
  transitStore.selectedRailDirection = null
  transitStore.arrivals = []
  transitStore.resultMeta = null
  transitStore.error = null
}

function refreshArrivals(): void {
  void transitStore.refreshRailArrivals(cityStore.currentCity.id)
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
    (!arrival.estimatedAt || Date.parse(arrival.scheduledAt!) !== Date.parse(arrival.estimatedAt))
  if (arrival.isLastService) labels.push('末班車')

  if (view.mode === 'realtime') {
    labels.push('即時預估')
    if (arrival.serviceStatus && !['即時預估', '正常營運'].includes(arrival.serviceStatus)) {
      labels.push(arrival.serviceStatus)
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

function directionLabel(direction: TransitDirection): string {
  return getRailDirectionLabel(
    cityStore.currentCity.countryCode,
    direction,
    transitStore.arrivals,
  )
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
  <div class="content-stack rail-page">
    <section class="page-intro transit-intro">
      <div>
        <StatusPill :tone="statusTone" :label="statusLabel" />
        <h2>{{ pageLabel }}</h2>
        <p v-if="isTaiwanRail">
          先選擇台灣區域與縣市，再以按鈕選擇車站；班次資料由後端統一向 TDX 取得並共用快取。
        </p>
        <p v-else>
          依目前城市已整合的鐵路服務瀏覽區域、城市與車站，不會顯示尚未串接的鐵路入口。
        </p>
      </div>
      <div class="intro-icon"><MapPinned :size="34" /></div>
    </section>

    <section v-if="!railService" class="transit-workspace inline-empty standalone">
      {{ cityStore.currentCity.nameZh }}目前沒有已整合的鐵路查詢。
    </section>

    <section v-else-if="!railRegions.length" class="transit-workspace inline-empty standalone">
      {{ railService.displayName }}已列為整合服務，但此國家的區域選站流程尚未設定。
    </section>

    <section v-else class="transit-workspace rail-workspace">
      <div class="rail-choice-stack">
        <div class="rail-choice-group">
          <div class="panel-heading-row">
            <div>
              <span class="eyebrow">STEP 1</span>
              <h3>選擇區域</h3>
            </div>
          </div>
          <div class="rail-choice-grid">
            <button
              v-for="region in railRegions"
              :key="region.id"
              type="button"
              class="rail-choice-button"
              :class="{ active: selectedRegionId === region.id }"
              @click="chooseRegion(region.id)"
            >
              {{ region.name }}
            </button>
          </div>
        </div>

        <div v-if="selectedRegion" class="rail-choice-group">
          <div class="panel-heading-row">
            <div>
              <span class="eyebrow">STEP 2</span>
              <h3>選擇縣市</h3>
            </div>
          </div>
          <div class="rail-choice-grid rail-locality-grid">
            <button
              v-for="locality in selectedRegion.localities"
              :key="locality.name"
              type="button"
              class="rail-choice-button"
              :class="{ active: selectedLocality?.name === locality.name }"
              @click="chooseLocality(locality)"
            >
              {{ locality.name }}
            </button>
          </div>
        </div>
      </div>

      <div v-if="selectedLocality && transitStore.railStations.length" class="transit-results-layout">
        <section class="selection-panel">
          <div class="panel-heading-row">
            <div>
              <span class="eyebrow">STEP 3</span>
              <h3>選擇車站</h3>
            </div>
            <span>{{ transitStore.railStations.length }} 站</span>
          </div>
          <div class="route-result-list">
            <button
              v-for="station in orderedRailStations"
              :key="station.id"
              class="route-result"
              :class="{ selected: transitStore.selectedRailStation?.id === station.id }"
              type="button"
              @click="chooseStation(station)"
            >
              <strong>{{ station.nameZh }}</strong>
              <span>{{ station.nameEn ?? station.address ?? railService.displayName }}</span>
              <small>{{ station.id }}</small>
            </button>
          </div>
        </section>

        <section class="arrival-panel">
          <template v-if="transitStore.selectedRailStation">
            <div class="panel-heading-row arrival-heading">
              <div>
                <span class="eyebrow">{{ transitStore.selectedRailStation.id }}</span>
                <h3>{{ transitStore.selectedRailStation.nameZh }}列車資訊</h3>
              </div>
              <button
                class="icon-button refresh-button"
                type="button"
                aria-label="更新鐵路列車資訊"
                :disabled="transitStore.loading"
                @click="refreshArrivals"
              >
                <RefreshCw :size="18" />
              </button>
            </div>

            <div
              v-if="transitStore.railDirectionOptions.length"
              class="direction-switch metro-direction-switch"
              aria-label="鐵路方向"
            >
              <button
                v-for="direction in transitStore.railDirectionOptions"
                :key="direction.direction"
                type="button"
                :class="{ active: transitStore.selectedRailDirection === direction.direction }"
                @click="chooseDirection(direction.direction)"
              >
                {{ directionLabel(direction) }}
              </button>
            </div>

            <div class="arrival-list metro-arrivals">
              <article
                v-for="arrival in transitStore.visibleRailArrivals"
                :key="arrival.id"
                class="arrival-card"
              >
                <div class="arrival-icon"><TrainFront :size="20" /></div>
                <div class="arrival-main">
                  <strong>
                    {{ arrival.routeName ? `${arrival.routeName} 次` : '車次待確認' }}
                    · {{ arrival.lineName ?? '鐵路列車' }}
                  </strong>
                  <span>
                    {{ arrival.destinationName ? `往 ${arrival.destinationName}` : arrival.serviceStatus }}
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
              <div v-if="!transitStore.visibleRailArrivals.length && !transitStore.loading" class="inline-empty">
                {{
                  transitStore.error ??
                  transitStore.resultMeta?.message ??
                  '目前查無這個車站的列車資料。'
                }}
              </div>
            </div>
          </template>
          <div v-else class="inline-empty large">請從左側選擇一座車站。</div>
        </section>
      </div>

      <div v-else-if="selectedLocality && !transitStore.loading" class="inline-empty standalone">
        {{
          transitStore.error ??
          transitStore.resultMeta?.message ??
          `${selectedLocality.name}目前查無可選車站。`
        }}
      </div>

      <footer v-if="transitStore.resultMeta" class="data-source-footer">
        <Database :size="15" />
        <span>
          資料來源 {{ transitStore.resultMeta.source }} ·
          更新 {{ formatTimestamp(transitStore.resultMeta.sourceUpdatedAt) }}
        </span>
        <span v-if="transitStore.resultMeta.stale" class="stale-badge">快取備援</span>
        <span v-if="transitStore.resultMeta.message">{{ transitStore.resultMeta.message }}</span>
      </footer>
    </section>
  </div>
</template>
