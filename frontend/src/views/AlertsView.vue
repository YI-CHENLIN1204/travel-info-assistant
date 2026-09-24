<script setup lang="ts">
import { computed, watch } from 'vue'
import {
  AlertTriangle,
  ArrowUpRight,
  Database,
  MapPin,
  RefreshCw,
  ShieldAlert,
} from '@lucide/vue'
import StatusPill from '@/components/StatusPill.vue'
import { getAlertAction, getAlertTone } from '@/services/alertDisplay'
import { useAlertStore } from '@/stores/alerts'
import { useCityStore } from '@/stores/city'
import type { TravelAlert } from '@/types/api'

const cityStore = useCityStore()
const alertStore = useAlertStore()

const sourceUpdatedAt = computed(
  () => alertStore.popularMeta?.sourceUpdatedAt ?? alertStore.popularMeta?.fetchedAt ?? null,
)

watch(
  () => cityStore.selectedCityId,
  (cityId) => {
    if (cityId) void alertStore.load(cityId)
  },
  { immediate: true },
)

function formatDate(value: string | null): string {
  if (!value) return '尚未提供'
  return new Intl.DateTimeFormat('zh-TW', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    timeZone: 'Asia/Taipei',
  }).format(new Date(value))
}

function alertKey(alert: TravelAlert): string {
  return `${alert.countryCode}-${alert.id}-${alert.regionName}`
}
</script>

<template>
  <div class="content-stack alerts-page">
    <section class="page-intro alerts-intro">
      <div>
        <StatusPill tone="ready" label="BOCA 官方警示已啟用" />
        <h2>旅遊警示</h2>
        <p>依目前城市與熱門目的地整理外交部領事事務局公告；警示僅供行前判斷，請開啟官方來源閱讀完整內容。</p>
      </div>
      <div class="intro-icon"><ShieldAlert :size="34" /></div>
    </section>

    <div
      v-if="alertStore.popularMeta?.message && alertStore.popularAlerts.length"
      class="provider-message provider-message-warning"
      role="status"
    >
      <RefreshCw :size="18" />
      <span>{{ alertStore.popularMeta.message }}</span>
    </div>

    <section class="alert-section-panel">
      <div class="panel-heading-row">
        <div>
          <span class="eyebrow">CURRENT CITY</span>
          <h3>{{ cityStore.currentCity.nameZh }}所屬地區</h3>
        </div>
        <MapPin :size="20" />
      </div>

      <div v-if="alertStore.cityAlerts.length" class="alert-list">
        <article
          v-for="alert in alertStore.cityAlerts"
          :key="alertKey(alert)"
          class="alert-card"
          :class="`alert-${getAlertTone(alert.level)}`"
        >
          <div class="alert-card-header">
            <div>
              <span class="alert-level">{{ alert.levelLabel }}</span>
              <h4>{{ alert.regionName }}</h4>
              <small>{{ alert.countryNameEn }}</small>
            </div>
            <strong>{{ getAlertAction(alert.level) }}</strong>
          </div>
          <p>{{ alert.summary }}</p>
          <div class="alert-card-footer">
            <span>更新 {{ formatDate(alert.updatedAt) }}</span>
            <a :href="alert.sourceUrl" target="_blank" rel="noreferrer">
              官方全文 <ArrowUpRight :size="14" />
            </a>
          </div>
        </article>
      </div>

      <div v-else-if="!alertStore.loading" class="alert-empty-note">
        <ShieldAlert :size="24" />
        <div>
          <strong>目前城市沒有對應的國外旅遊警示</strong>
          <p>{{ alertStore.cityMeta?.message ?? '請參考下方熱門目的地的最新警示。' }}</p>
        </div>
      </div>
    </section>

    <section class="alert-section-panel">
      <div class="panel-heading-row">
        <div>
          <span class="eyebrow">POPULAR DESTINATIONS</span>
          <h3>熱門目的地最高警示</h3>
        </div>
        <AlertTriangle :size="20" />
      </div>

      <div v-if="alertStore.popularAlerts.length" class="alert-grid">
        <article
          v-for="alert in alertStore.popularAlerts"
          :key="alertKey(alert)"
          class="alert-card compact-alert-card"
          :class="`alert-${getAlertTone(alert.level)}`"
        >
          <div class="alert-card-header">
            <div>
              <span class="alert-level">第 {{ alert.level }} 級</span>
              <h4>{{ alert.countryNameZh }}</h4>
              <small>{{ alert.regionName }}</small>
            </div>
            <strong>{{ getAlertAction(alert.level) }}</strong>
          </div>
          <p>{{ alert.summary }}</p>
          <div class="alert-card-footer">
            <span>{{ formatDate(alert.updatedAt) }}</span>
            <a :href="alert.sourceUrl" target="_blank" rel="noreferrer" :aria-label="`開啟${alert.countryNameZh}官方警示`">
              BOCA <ArrowUpRight :size="14" />
            </a>
          </div>
        </article>
      </div>

      <div v-else-if="alertStore.loading" class="alert-loading" aria-live="polite">
        <RefreshCw :size="26" />
        <span>正在取得外交部最新旅遊警示</span>
      </div>

      <div v-else class="alert-empty-note" aria-live="polite">
        <AlertTriangle :size="24" />
        <div>
          <strong>官方警示暫時無法顯示</strong>
          <p>{{ alertStore.error ?? '請稍後再試。' }}</p>
        </div>
        <button
          v-if="cityStore.selectedCityId"
          class="button button-primary"
          type="button"
          @click="alertStore.load(cityStore.selectedCityId)"
        >
          重新整理
        </button>
      </div>

      <div v-if="sourceUpdatedAt" class="data-source-footer alerts-source-footer">
        <Database :size="14" />
        <span>
          資料來源
          <a href="https://www.boca.gov.tw/sp-trwa-list-1.html" target="_blank" rel="noreferrer">外交部領事事務局</a>
          · 更新 {{ formatDate(sourceUpdatedAt) }}
        </span>
        <span v-if="alertStore.popularMeta?.stale" class="stale-badge">備援快取</span>
      </div>
    </section>
  </div>
</template>

<style scoped>
.alerts-page { gap: 24px; }
.alerts-intro { background: linear-gradient(135deg, #fff 0%, #fff8f2 100%); }
.alert-section-panel { padding: 26px; border: 1px solid var(--line); border-radius: 22px; background: #fff; }
.alert-list, .alert-grid { display: grid; gap: 14px; margin-top: 20px; }
.alert-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
.alert-card { --alert-color: #64748b; --alert-soft: #f1f5f9; padding: 20px; border: 1px solid color-mix(in srgb, var(--alert-color) 28%, var(--line)); border-left: 5px solid var(--alert-color); border-radius: 17px; background: linear-gradient(135deg, #fff 55%, var(--alert-soft)); }
.alert-card.alert-yellow { --alert-color: #b7791f; --alert-soft: #fff8df; }
.alert-card.alert-orange { --alert-color: #c45a18; --alert-soft: #fff0e6; }
.alert-card.alert-red { --alert-color: #b42318; --alert-soft: #ffebe9; }
.alert-card-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; }
.alert-card-header h4 { margin: 7px 0 2px; font-size: 1.15rem; color: var(--navy); }
.alert-card-header small { color: var(--muted); }
.alert-card-header > strong { flex: 0 0 auto; color: var(--alert-color); font-size: .82rem; }
.alert-level { display: inline-flex; padding: 4px 9px; border-radius: 999px; background: var(--alert-soft); color: var(--alert-color); font-size: .75rem; font-weight: 800; }
.alert-card > p { margin: 15px 0; color: var(--ink); line-height: 1.7; }
.compact-alert-card > p { display: -webkit-box; overflow: hidden; -webkit-box-orient: vertical; -webkit-line-clamp: 3; }
.alert-card-footer { display: flex; align-items: center; justify-content: space-between; gap: 14px; padding-top: 13px; border-top: 1px solid var(--line); color: var(--muted); font-size: .78rem; }
.alert-card-footer a { display: inline-flex; align-items: center; gap: 4px; color: var(--teal); font-weight: 800; text-decoration: none; }
.alert-empty-note, .alert-loading { display: flex; align-items: center; gap: 14px; margin-top: 20px; padding: 20px; border: 1px dashed var(--line); border-radius: 16px; background: var(--canvas); color: var(--muted); }
.alert-empty-note strong { color: var(--navy); }
.alert-empty-note p { margin: 4px 0 0; }
.alert-empty-note .button { margin-left: auto; }
.alerts-source-footer { margin-top: 20px; }
@media (max-width: 760px) {
  .alert-section-panel { padding: 20px; }
  .alert-grid { grid-template-columns: 1fr; }
  .alert-card-header { align-items: flex-start; }
  .alert-empty-note { align-items: flex-start; flex-wrap: wrap; }
  .alert-empty-note .button { width: 100%; margin-left: 0; }
}
</style>
