<script setup lang="ts">
import { computed } from 'vue'
import { CloudSun, ShieldAlert, Siren } from '@lucide/vue'
import StatusPill from '@/components/StatusPill.vue'
import { useCityStore } from '@/stores/city'

const props = defineProps<{ module: 'weather' | 'alerts' | 'emergency' }>()
const cityStore = useCityStore()

const content = computed(() => {
  const modules = {
    weather: {
      title: `${cityStore.currentCity.nameZh}天氣`,
      description: '未來將透過 MET Norway 顯示目前氣溫、降雨、風速與預報摘要。',
      detail: '資料會依 Expires 更新，並由後端共用快取。',
      icon: CloudSun,
    },
    alerts: {
      title: '旅遊警示',
      description: '未來將顯示目前位置、選擇城市及熱門目的地的官方旅遊警示。',
      detail: '只收錄外交部與可追溯的官方安全資訊。',
      icon: ShieldAlert,
    },
    emergency: {
      title: `${cityStore.currentCity.nameZh}應急資訊`,
      description: '未來將提供警察、救護、消防、駐外館處及護照遺失處理指引。',
      detail: '緊急電話只以文字顯示，每筆資料附來源與確認日期。',
      icon: Siren,
    },
  }
  return modules[props.module]
})
</script>

<template>
  <div class="content-stack">
    <section class="page-intro">
      <div>
        <StatusPill tone="planned" label="Phase 5" />
        <h2>{{ content.title }}</h2>
        <p>{{ content.description }}</p>
      </div>
      <div class="intro-icon"><component :is="content.icon" :size="34" /></div>
    </section>

    <section class="empty-state">
      <span class="empty-state-icon"><component :is="content.icon" :size="34" /></span>
      <h3>功能入口已建立</h3>
      <p>{{ content.detail }}</p>
      <div class="next-step-note">目前版本不顯示任何模擬即時資料。</div>
    </section>
  </div>
</template>
