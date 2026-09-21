<script setup lang="ts">
import { computed } from 'vue'
import { BusFront, Clock3, MapPinned } from '@lucide/vue'
import StatusPill from '@/components/StatusPill.vue'
import { useCityStore } from '@/stores/city'

const cityStore = useCityStore()

const integratedServices = computed(() =>
  cityStore.currentCity.services.filter((service) => service.integrationStatus === 'integrated'),
)
</script>

<template>
  <div class="content-stack">
    <section class="page-intro">
      <div>
        <StatusPill tone="planned" label="Provider 尚未串接" />
        <h2>{{ cityStore.currentCity.nameZh }}大眾運輸</h2>
        <p>只顯示系統已完成串接的交通服務。當地存在但尚未整合的服務，不會被誤標為不存在。</p>
      </div>
      <div class="intro-icon"><MapPinned :size="34" /></div>
    </section>

    <section v-if="integratedServices.length" class="service-list">
      <article v-for="service in integratedServices" :key="service.serviceKey" class="service-card">
        <BusFront :size="24" />
        <div>
          <h3>{{ service.displayName }}</h3>
          <p>班次與到站資訊</p>
        </div>
        <Clock3 :size="19" />
      </article>
    </section>

    <section v-else class="empty-state">
      <span class="empty-state-icon"><BusFront :size="34" /></span>
      <h3>目前尚無已整合的交通入口</h3>
      <p>這不是代表{{ cityStore.currentCity.nameZh }}沒有大眾運輸，而是目前版本尚未完成資料源串接。</p>
      <div class="next-step-note">
        下一步將先完成台北 TDX，再加入東京 ODPT。
      </div>
    </section>

    <section class="rule-panel">
      <div>
        <span class="eyebrow">ARRIVAL RULE</span>
        <h3>到站資訊顯示原則</h3>
      </div>
      <ul>
        <li><strong>60 分鐘以上</strong><span>顯示表定時間</span></li>
        <li><strong>60 分鐘內</strong><span>顯示即時預估</span></li>
        <li><strong>少於 1 分鐘</strong><span>顯示「即將進站」</span></li>
        <li><strong>即時資料過期</strong><span>自動降級為表定時間</span></li>
      </ul>
    </section>
  </div>
</template>
