<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import {
  BusFront,
  ChevronLeft,
  CloudSun,
  Compass,
  Home,
  MapPin,
  Menu,
  PanelLeftOpen,
  Plane,
  ShieldAlert,
  Siren,
  X,
} from '@lucide/vue'
import { useAppStore } from '@/stores/app'
import { useCityStore } from '@/stores/city'

const route = useRoute()
const appStore = useAppStore()
const cityStore = useCityStore()

const collapsed = ref(false)
const mobileOpen = ref(false)

const navigation = [
  { to: '/', label: '首頁', icon: Home },
  { to: '/transit', label: '大眾運輸', icon: BusFront },
  { to: '/flights', label: '航班查詢', icon: Plane },
  { to: '/weather', label: '天氣', icon: CloudSun },
  { to: '/alerts', label: '旅遊警示', icon: ShieldAlert },
  { to: '/emergency', label: '應急資訊', icon: Siren },
]

const pageTitle = computed(() => String(route.meta.title ?? '旅途通'))
const apiLabel = computed(() => {
  const labels = {
    checking: '檢查服務中',
    online: '系統正常',
    degraded: '部分服務異常',
    offline: '後端未連線',
  }
  return labels[appStore.apiStatus]
})

watch(
  () => route.fullPath,
  () => {
    mobileOpen.value = false
  },
)

function onCityChange(event: Event): void {
  cityStore.selectCity((event.target as HTMLSelectElement).value)
}
</script>

<template>
  <div class="app-shell" :class="{ 'sidebar-collapsed': collapsed }">
    <div
      v-if="mobileOpen"
      class="sidebar-backdrop"
      aria-hidden="true"
      @click="mobileOpen = false"
    />

    <aside class="sidebar" :class="{ 'mobile-open': mobileOpen }">
      <div class="brand-row">
        <RouterLink class="brand" to="/" aria-label="旅途通首頁">
          <span class="brand-mark"><Compass :size="22" /></span>
          <span v-if="!collapsed" class="brand-copy">
            <strong>旅途通</strong>
            <small>Travel Info</small>
          </span>
        </RouterLink>
        <button class="mobile-close icon-button" type="button" aria-label="關閉選單" @click="mobileOpen = false">
          <X :size="20" />
        </button>
      </div>

      <p v-if="!collapsed" class="nav-caption">旅遊工具</p>
      <nav class="primary-nav" aria-label="主要導覽">
        <RouterLink
          v-for="item in navigation"
          :key="item.to"
          :to="item.to"
          class="nav-item"
          :title="collapsed ? item.label : undefined"
        >
          <component :is="item.icon" :size="20" />
          <span v-if="!collapsed">{{ item.label }}</span>
        </RouterLink>
      </nav>

      <div class="sidebar-footer">
        <div class="system-indicator" :title="apiLabel">
          <span class="status-dot" :class="`status-${appStore.apiStatus}`" />
          <span v-if="!collapsed">{{ apiLabel }}</span>
        </div>
        <button
          class="collapse-button"
          type="button"
          :aria-label="collapsed ? '展開側邊欄' : '收合側邊欄'"
          @click="collapsed = !collapsed"
        >
          <ChevronLeft v-if="!collapsed" :size="18" />
          <PanelLeftOpen v-else :size="18" />
          <span v-if="!collapsed">收合選單</span>
        </button>
      </div>
    </aside>

    <section class="workspace">
      <header class="topbar">
        <div class="topbar-title">
          <button class="mobile-menu icon-button" type="button" aria-label="開啟選單" @click="mobileOpen = true">
            <Menu :size="22" />
          </button>
          <div>
            <span class="eyebrow">TRAVEL ASSISTANT</span>
            <h1>{{ pageTitle }}</h1>
          </div>
        </div>

        <label class="city-selector">
          <MapPin :size="18" />
          <span class="sr-only">目前城市</span>
          <select
            :value="cityStore.selectedCityId ?? ''"
            :disabled="cityStore.loading"
            aria-label="選擇城市"
            @change="onCityChange"
          >
            <option v-for="city in cityStore.cities" :key="city.id" :value="city.id">
              {{ city.nameZh }} · {{ city.nameEn }}
            </option>
          </select>
        </label>
      </header>

      <div v-if="cityStore.suggestedCity" class="context-banner location-suggestion">
        <div>
          <strong>偵測到你目前可能在{{ cityStore.suggestedCity.nameZh }}</strong>
          <span>要將旅遊資訊切換到這座城市嗎？</span>
        </div>
        <div class="banner-actions">
          <button class="button button-ghost" type="button" @click="cityStore.dismissSuggestedCity">維持目前城市</button>
          <button class="button button-primary" type="button" @click="cityStore.acceptSuggestedCity">切換城市</button>
        </div>
      </div>

      <div v-else-if="cityStore.locationStatus === 'denied'" class="context-banner permission-banner">
        <MapPin :size="18" />
        <span>定位權限目前未開啟；系統會繼續使用上次選擇的城市。</span>
        <button class="text-button" type="button" @click="cityStore.requestLocation">重新嘗試</button>
      </div>

      <main class="page-content">
        <RouterView />
      </main>
    </section>
  </div>
</template>
