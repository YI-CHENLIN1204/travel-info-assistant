<script setup lang="ts">
import { computed, onMounted } from 'vue'
import {
  ArrowLeftRight,
  CalendarDays,
  CircleAlert,
  Database,
  Gauge,
  Plane,
  Search,
} from '@lucide/vue'
import AirportAutocomplete from '@/components/AirportAutocomplete.vue'
import StatusPill from '@/components/StatusPill.vue'
import { useFlightsStore, type FlightSearchMode } from '@/stores/flights'
import type { FlightMovement, FlightSegment, FlightTime } from '@/types/api'

const flightsStore = useFlightsStore()

const providerConfigured = computed(() => flightsStore.providerStatus?.configured === true)
const statusTone = computed<'ready' | 'warning'>(() =>
  providerConfigured.value ? 'ready' : 'warning',
)
const statusLabel = computed(() =>
  providerConfigured.value ? '全球真實航班已啟用' : 'AeroDataBox 金鑰待設定',
)
const quotaPercent = computed(() => {
  const status = flightsStore.providerStatus
  if (!status || status.softLimitUnits <= 0) return 0
  return Math.min(100, (status.usedUnits / status.softLimitUnits) * 100)
})

onMounted(() => void flightsStore.loadProviderStatus())

function activateMode(mode: FlightSearchMode): void {
  flightsStore.setMode(mode)
}

function submit(): void {
  if (flightsStore.searchMode === 'route') {
    void flightsStore.searchRoute()
  } else {
    void flightsStore.searchNumber()
  }
}

function segmentOf(index: number): FlightSegment | null {
  return flightsStore.itineraries[index]?.segments[0] ?? null
}

function bestTime(movement: FlightMovement): { value: FlightTime | null; label: string } {
  if (movement.actual) return { value: movement.actual, label: '實際' }
  if (movement.estimated) return { value: movement.estimated, label: '預估' }
  return { value: movement.scheduled, label: '表定' }
}

function formatTime(value: FlightTime | null): string {
  if (!value) return '--:--'
  const local = value.local?.replace(' ', 'T')
  const match = local?.match(/T(\d{2}:\d{2})/)
  if (match?.[1]) return match[1]

  if (value.utc) {
    return new Intl.DateTimeFormat('zh-TW', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
      timeZone: 'UTC',
    }).format(new Date(value.utc))
  }
  return '--:--'
}

function datePart(value: FlightTime | null): string | null {
  if (value?.local) return value.local.slice(0, 10)
  if (value?.utc) return value.utc.slice(0, 10)
  return null
}

function dayOffset(segment: FlightSegment): string | null {
  const departure = datePart(bestTime(segment.departure).value)
  const arrival = datePart(bestTime(segment.arrival).value)
  if (!departure || !arrival) return null

  const days = Math.round(
    (Date.parse(`${arrival}T00:00:00Z`) - Date.parse(`${departure}T00:00:00Z`)) / 86_400_000,
  )
  if (days === 0) return null
  return days > 0 ? `+${days} 日` : `${days} 日`
}

function statusLabelFor(status: string): string {
  const labels: Record<string, string> = {
    Unknown: '狀態待確認',
    Expected: '預計準時',
    EnRoute: '飛行中',
    CheckIn: '報到中',
    Boarding: '登機中',
    GateClosed: '登機門關閉',
    Departed: '已起飛',
    Delayed: '延誤',
    Approaching: '即將抵達',
    Arrived: '已抵達',
    Canceled: '已取消',
    Diverted: '轉降',
    CanceledUncertain: '可能取消',
  }
  return labels[status] ?? status
}

function statusToneFor(status: string): string {
  if (status === 'Canceled' || status === 'CanceledUncertain' || status === 'Diverted') {
    return 'danger'
  }
  if (status === 'Delayed') return 'warning'
  if (['EnRoute', 'Departed', 'Approaching', 'Arrived'].includes(status)) return 'live'
  return 'scheduled'
}

function formatTimestamp(value: string | null | undefined): string {
  if (!value) return '尚無更新時間'
  return new Intl.DateTimeFormat('zh-TW', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(new Date(value))
}

function formatBytes(value: number): string {
  if (value < 1_000_000) return `${(value / 1_000).toFixed(1)} KB`
  return `${(value / 1_000_000).toFixed(1)} MB`
}
</script>

<template>
  <div class="content-stack">
    <section class="page-intro flight-intro">
      <div>
        <StatusPill :tone="statusTone" :label="statusLabel" />
        <h2>查詢全球直飛航班</h2>
        <p>選擇世界任意兩座定期航班機場，或直接輸入航班編號；第一版只顯示直飛，不包含票價與訂位。</p>
      </div>
      <div class="intro-icon"><Plane :size="34" /></div>
    </section>

    <section class="search-panel flight-search-panel">
      <div class="search-tabs" role="tablist" aria-label="航班查詢方式">
        <button
          class="search-tab"
          :class="{ active: flightsStore.searchMode === 'route' }"
          type="button"
          role="tab"
          :aria-selected="flightsStore.searchMode === 'route'"
          @click="activateMode('route')"
        >
          機場到機場
        </button>
        <button
          class="search-tab"
          :class="{ active: flightsStore.searchMode === 'number' }"
          type="button"
          role="tab"
          :aria-selected="flightsStore.searchMode === 'number'"
          @click="activateMode('number')"
        >
          航班編號
        </button>
      </div>

      <form
        class="flight-form"
        :class="{ 'flight-form-number': flightsStore.searchMode === 'number' }"
        @submit.prevent="submit"
      >
        <template v-if="flightsStore.searchMode === 'route'">
          <AirportAutocomplete
            v-model="flightsStore.origin"
            label="出發機場"
            placeholder="輸入城市、機場或 TPE"
          />

          <button
            class="flight-direction"
            type="button"
            aria-label="交換出發與抵達機場"
            @click="flightsStore.swapAirports"
          >
            <ArrowLeftRight :size="20" />
          </button>

          <AirportAutocomplete
            v-model="flightsStore.destination"
            label="抵達機場"
            placeholder="輸入城市、機場或 NRT"
          />
        </template>

        <label v-else class="flight-number-field">
          <span>航班編號</span>
          <span class="input-shell">
            <Plane :size="17" />
            <input
              v-model.trim="flightsStore.flightNumber"
              maxlength="10"
              placeholder="例如 BR198"
              autocomplete="off"
            />
          </span>
        </label>

        <label class="flight-date-field">
          <span>出發日期</span>
          <span class="input-shell">
            <CalendarDays :size="17" />
            <input v-model="flightsStore.date" type="date" />
          </span>
        </label>

        <button class="button button-primary search-button" type="submit" :disabled="flightsStore.loading">
          <Search :size="17" />
          {{ flightsStore.loading ? '查詢中' : '查詢航班' }}
        </button>
      </form>

      <div v-if="!providerConfigured" class="provider-message provider-message-warning flight-provider-message">
        <CircleAlert :size="19" />
        <span>機場搜尋可直接使用；設定 AeroDataBox 金鑰後才會發出真實航班查詢。</span>
      </div>
      <div v-if="flightsStore.error" class="provider-message provider-message-error flight-provider-message">
        <CircleAlert :size="19" />
        <span>{{ flightsStore.error }}</span>
      </div>
    </section>

    <section v-if="flightsStore.itineraries.length" class="flight-results">
      <div class="panel-heading-row">
        <div>
          <span class="eyebrow">DIRECT FLIGHTS</span>
          <h3>直飛航班</h3>
        </div>
        <span>{{ flightsStore.itineraries.length }} 筆</span>
      </div>

      <div class="flight-card-list">
        <article
          v-for="(itinerary, index) in flightsStore.itineraries"
          :key="itinerary.id"
          class="flight-card"
        >
          <template v-if="segmentOf(index)">
            <header class="flight-card-heading">
              <div class="flight-airline">
                <span class="flight-card-icon"><Plane :size="20" /></span>
                <div>
                  <strong>{{ segmentOf(index)!.flightNumber }}</strong>
                  <span>{{ segmentOf(index)!.airline?.name ?? '航空公司待確認' }}</span>
                </div>
              </div>
              <span
                class="flight-status"
                :class="`flight-status-${statusToneFor(segmentOf(index)!.status)}`"
              >
                {{ statusLabelFor(segmentOf(index)!.status) }}
              </span>
            </header>

            <div class="flight-timeline">
              <div class="flight-endpoint">
                <span>{{ bestTime(segmentOf(index)!.departure).label }}</span>
                <strong>{{ formatTime(bestTime(segmentOf(index)!.departure).value) }}</strong>
                <b>{{ segmentOf(index)!.departure.airport.iata }}</b>
                <small>{{ segmentOf(index)!.departure.airport.municipality ?? segmentOf(index)!.departure.airport.name }}</small>
              </div>

              <div class="flight-path" aria-hidden="true">
                <Plane :size="18" />
                <small>直飛</small>
              </div>

              <div class="flight-endpoint flight-endpoint-arrival">
                <span>
                  {{ bestTime(segmentOf(index)!.arrival).label }}
                  <em v-if="dayOffset(segmentOf(index)!)">{{ dayOffset(segmentOf(index)!) }}</em>
                </span>
                <strong>{{ formatTime(bestTime(segmentOf(index)!.arrival).value) }}</strong>
                <b>{{ segmentOf(index)!.arrival.airport.iata }}</b>
                <small>{{ segmentOf(index)!.arrival.airport.municipality ?? segmentOf(index)!.arrival.airport.name }}</small>
              </div>
            </div>

            <footer class="flight-card-details">
              <span>
                出發航廈 <strong>{{ segmentOf(index)!.departure.terminal ?? '尚未提供' }}</strong>
              </span>
              <span>
                登機門 <strong>{{ segmentOf(index)!.departure.gate ?? '尚未提供' }}</strong>
              </span>
              <span>
                抵達航廈 <strong>{{ segmentOf(index)!.arrival.terminal ?? '尚未提供' }}</strong>
              </span>
              <span v-if="segmentOf(index)!.aircraft?.model">
                機型 <strong>{{ segmentOf(index)!.aircraft!.model }}</strong>
              </span>
            </footer>
          </template>
        </article>
      </div>

      <footer v-if="flightsStore.resultMeta" class="data-source-footer">
        <Database :size="15" />
        <span>
          資料來源 {{ flightsStore.resultMeta.source }} ·
          更新 {{ formatTimestamp(flightsStore.resultMeta.sourceUpdatedAt ?? flightsStore.resultMeta.fetchedAt) }}
        </span>
        <span v-if="flightsStore.resultMeta.stale" class="stale-badge">快取備援</span>
        <span v-if="flightsStore.resultMeta.message">{{ flightsStore.resultMeta.message }}</span>
      </footer>
    </section>

    <section v-else-if="flightsStore.searched && !flightsStore.loading" class="empty-state compact-empty">
      <span class="empty-state-icon"><Plane :size="34" /></span>
      <h3>目前查無符合的直飛航班</h3>
      <p>{{ flightsStore.resultMeta?.message ?? '請確認機場、日期或航班編號後再試一次。' }}</p>
    </section>

    <section v-else class="empty-state compact-empty">
      <span class="empty-state-icon"><Plane :size="34" /></span>
      <h3>從機場或航班編號開始</h3>
      <p>機場名稱由本地資料搜尋，不會消耗外部 API 額度；送出後只查詢真實航班資料。</p>
    </section>

    <section class="flight-insights">
      <article class="quota-panel flight-quota-panel">
        <div class="panel-icon"><Gauge :size="21" /></div>
        <div>
          <span class="eyebrow">FREE QUOTA GUARD</span>
          <h3>AeroDataBox 額度保護</h3>
        </div>
        <template v-if="flightsStore.providerStatus">
          <div class="quota-numbers">
            <strong>{{ flightsStore.providerStatus.usedUnits }}</strong>
            <span>/ {{ flightsStore.providerStatus.softLimitUnits }} units 內部停止線</span>
          </div>
          <div class="quota-track" aria-label="本期 AeroDataBox 使用率">
            <span :style="{ width: `${quotaPercent}%` }" />
          </div>
          <p>
            週期 {{ flightsStore.providerStatus.billingCycle }} 已記錄
            {{ flightsStore.providerStatus.requestCount }} 次外部請求、
            {{ formatBytes(flightsStore.providerStatus.responseBytes) }}；付費超額已關閉。方案查核：
            {{ flightsStore.providerStatus.pricingVerifiedAt }}。
          </p>
        </template>
        <p v-else>後端未連線，暫時無法讀取用量估算。</p>
      </article>

      <article class="rule-panel flight-rule-panel">
        <div>
          <span class="eyebrow">SEARCH SCOPE</span>
          <h3>第一版查詢範圍</h3>
        </div>
        <ul>
          <li><strong>全球機場</strong><span>本地機場字典自動完成</span></li>
          <li><strong>只查直飛</strong><span>不組合轉機與票價</span></li>
          <li><strong>當地時間</strong><span>跨日顯示 +1 日</span></li>
          <li><strong>真實資料</strong><span>來源中斷不補假航班</span></li>
        </ul>
      </article>
    </section>
  </div>
</template>
