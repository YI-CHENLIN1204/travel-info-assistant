<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import {
  ArrowUpRight,
  BusFront,
  CloudSun,
  Database,
  Plane,
  ShieldAlert,
  Siren,
} from '@lucide/vue'
import StatusPill from '@/components/StatusPill.vue'
import { useAppStore } from '@/stores/app'
import { useCityStore } from '@/stores/city'

const cityStore = useCityStore()
const appStore = useAppStore()

const modules = [
  {
    title: '大眾運輸',
    description: '依城市查詢公車、捷運與火車班次。',
    to: '/transit',
    icon: BusFront,
    accent: 'teal',
    detail: 'MVP · 台北 TDX 真實資料',
  },
  {
    title: '全球航班',
    description: '查詢全球任意兩座機場間的直飛航班。',
    to: '/flights',
    icon: Plane,
    accent: 'blue',
    detail: 'Phase 4 串接真實資料',
  },
  {
    title: '天氣',
    description: '查看目前城市或定位地點的天氣摘要。',
    to: '/weather',
    icon: CloudSun,
    accent: 'amber',
    detail: 'Phase 5 · MET Norway 真實資料',
  },
  {
    title: '旅遊警示',
    description: '彙整官方旅遊警示與安全相關消息。',
    to: '/alerts',
    icon: ShieldAlert,
    accent: 'coral',
    detail: 'Phase 5 · BOCA 官方警示',
  },
  {
    title: '應急資訊',
    description: '查詢當地緊急電話、駐外館處與處理指引。',
    to: '/emergency',
    icon: Siren,
    accent: 'violet',
    detail: 'Phase 5 尚未整合',
  },
]

const integratedServices = computed(() =>
  cityStore.currentCity.services.filter((service) => service.integrationStatus === 'integrated'),
)

const systemTone = computed(() => (appStore.apiStatus === 'online' ? 'ready' : 'warning'))
</script>

<template>
  <div class="home-page">
    <section class="welcome-panel">
      <div class="welcome-copy">
        <StatusPill :tone="systemTone" :label="appStore.apiStatus === 'online' ? '系統連線正常' : '開發環境未連線'" />
        <h2>從{{ cityStore.currentCity.nameZh }}出發，<br />旅途中需要的資訊都在這裡。</h2>
        <p>
          MVP 已提供台北大眾運輸、全球直飛航班與天氣預報。所有資料都會標示來源與更新時間，尚未串接的服務不會用假資料代替。
        </p>
      </div>
      <div class="welcome-visual" aria-hidden="true">
        <span class="route-line route-line-one" />
        <span class="route-line route-line-two" />
        <span class="route-point route-point-origin" />
        <span class="route-point route-point-destination" />
        <Plane :size="42" stroke-width="1.5" />
      </div>
    </section>

    <section class="section-block">
      <div class="section-heading">
        <div>
          <span class="eyebrow">QUICK ACCESS</span>
          <h2>旅遊工具</h2>
        </div>
        <span class="section-note">目前城市：{{ cityStore.currentCity.nameZh }}</span>
      </div>

      <div class="module-grid">
        <RouterLink v-for="item in modules" :key="item.to" :to="item.to" class="module-card">
          <div class="module-icon" :class="`accent-${item.accent}`">
            <component :is="item.icon" :size="23" />
          </div>
          <div class="module-copy">
            <div class="module-title-row">
              <h3>{{ item.title }}</h3>
              <ArrowUpRight :size="18" />
            </div>
            <p>{{ item.description }}</p>
            <small>{{ item.detail }}</small>
          </div>
        </RouterLink>
      </div>
    </section>

    <section class="dashboard-grid">
      <article class="info-panel">
        <div class="panel-icon"><BusFront :size="21" /></div>
        <div>
          <span class="eyebrow">CITY SERVICES</span>
          <h3>{{ cityStore.currentCity.nameZh }}已整合服務</h3>
        </div>
        <div v-if="integratedServices.length" class="service-tags">
          <span v-for="service in integratedServices" :key="service.serviceKey">
            {{ service.displayName }}
          </span>
        </div>
        <p v-else class="empty-copy">目前沒有已完成串接的交通入口；該城市完成資料源整合後才會顯示。</p>
      </article>

      <article class="info-panel architecture-panel">
        <div class="panel-icon"><Database :size="21" /></div>
        <div>
          <span class="eyebrow">MVP ARCHITECTURE</span>
          <h3>中央取得，共用快取</h3>
        </div>
        <p>前端只呼叫自有 API；第三方資料由後端統一取得、標準化並透過 Redis 共用。</p>
        <div class="architecture-flow" aria-label="前端到資料來源的流程">
          <span>LIFF</span><i /> <span>Web API</span><i /> <span>Cache</span><i /> <span>Provider</span>
        </div>
      </article>
    </section>
  </div>
</template>
