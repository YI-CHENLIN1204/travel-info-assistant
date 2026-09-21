<script setup lang="ts">
import { reactive, ref } from 'vue'
import { ArrowRight, CalendarDays, Plane, Search } from '@lucide/vue'
import StatusPill from '@/components/StatusPill.vue'

const form = reactive({ origin: '', destination: '', date: '', flightNumber: '' })
const developmentNotice = ref(false)

function submitSearch(): void {
  developmentNotice.value = true
}
</script>

<template>
  <div class="content-stack">
    <section class="page-intro flight-intro">
      <div>
        <StatusPill tone="planned" label="全球直飛 · Phase 4" />
        <h2>查詢世界任意兩座機場</h2>
        <p>航班查詢不受目前城市限制。第一版提供直飛航班與單一航班編號狀態，不包含訂位與票價。</p>
      </div>
      <div class="intro-icon"><Plane :size="34" /></div>
    </section>

    <section class="search-panel">
      <div class="search-tabs" role="tablist" aria-label="航班查詢方式">
        <button class="search-tab active" type="button">機場到機場</button>
        <button class="search-tab" type="button" disabled>航班編號</button>
      </div>

      <form class="flight-form" @submit.prevent="submitSearch">
        <label>
          <span>出發機場</span>
          <div class="input-shell">
            <Plane :size="17" />
            <input v-model.trim="form.origin" maxlength="3" placeholder="例如 TPE" autocomplete="off" />
          </div>
        </label>

        <span class="flight-direction" aria-hidden="true"><ArrowRight :size="20" /></span>

        <label>
          <span>抵達機場</span>
          <div class="input-shell">
            <Plane :size="17" />
            <input v-model.trim="form.destination" maxlength="3" placeholder="例如 NRT" autocomplete="off" />
          </div>
        </label>

        <label>
          <span>出發日期</span>
          <div class="input-shell">
            <CalendarDays :size="17" />
            <input v-model="form.date" type="date" />
          </div>
        </label>

        <button class="button button-primary search-button" type="submit">
          <Search :size="17" />
          查詢航班
        </button>
      </form>

      <p v-if="developmentNotice" class="development-notice">
        查詢介面已完成，但 AeroDataBox 尚未接入；目前不會回傳假航班資料。
      </p>
    </section>

    <section class="empty-state compact-empty">
      <span class="empty-state-icon"><Plane :size="34" /></span>
      <h3>真實航班資料將在 Phase 4 開放</h3>
      <p>屆時會顯示航班狀態、時間、航廈、登機門與資料更新時間，並套用免費額度防護。</p>
    </section>
  </div>
</template>
