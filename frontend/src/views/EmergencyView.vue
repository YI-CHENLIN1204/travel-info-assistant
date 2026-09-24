<script setup lang="ts">
import { watch } from 'vue'
import {
  AlertTriangle,
  ArrowUpRight,
  Building2,
  Database,
  FileText,
  Phone,
  RefreshCw,
  ShieldCheck,
  Siren,
} from '@lucide/vue'
import StatusPill from '@/components/StatusPill.vue'
import {
  formatVerifiedDate,
  getEmergencyCategoryLabel,
  getEmergencyCategoryTone,
} from '@/services/emergencyDisplay'
import { useCityStore } from '@/stores/city'
import { useEmergencyStore } from '@/stores/emergency'

const cityStore = useCityStore()
const emergencyStore = useEmergencyStore()

watch(
  () => cityStore.selectedCityId,
  (cityId) => {
    if (cityId) void emergencyStore.load(cityId)
  },
  { immediate: true },
)
</script>

<template>
  <div class="content-stack emergency-page">
    <section class="page-intro emergency-intro">
      <div>
        <StatusPill tone="ready" label="官方資料已人工確認" />
        <h2>{{ cityStore.currentCity.nameZh }}應急資訊</h2>
        <p>整理當地緊急電話、台灣駐外館處與常見事故處理步驟；發生立即危險時，請優先聯絡當地警消。</p>
      </div>
      <div class="intro-icon"><Siren :size="34" /></div>
    </section>

    <div class="safety-banner" role="note">
      <Phone :size="20" />
      <div>
        <strong>電話皆以純文字顯示，不會自動撥號</strong>
        <span>請依你所在位置、SIM 卡與漫遊狀態確認撥號格式；使用前再次核對官方來源。</span>
      </div>
    </div>

    <section v-if="emergencyStore.loading" class="state-panel" aria-live="polite">
      <RefreshCw :size="28" />
      <strong>正在載入應急資訊</strong>
    </section>

    <section v-else-if="emergencyStore.error || !emergencyStore.info" class="state-panel error-panel" aria-live="polite">
      <AlertTriangle :size="28" />
      <div>
        <strong>應急資訊暫時無法顯示</strong>
        <p>{{ emergencyStore.error ?? '請稍後再試。' }}</p>
      </div>
      <button
        v-if="cityStore.selectedCityId"
        class="button button-primary"
        type="button"
        @click="emergencyStore.load(cityStore.selectedCityId)"
      >
        重新整理
      </button>
    </section>

    <template v-else>
      <section class="emergency-section">
        <div class="panel-heading-row">
          <div>
            <span class="eyebrow">LOCAL EMERGENCY</span>
            <h3>{{ emergencyStore.info.locationName }}緊急聯絡電話</h3>
          </div>
          <ShieldCheck :size="21" />
        </div>

        <div class="contact-grid">
          <article
            v-for="contact in emergencyStore.info.contacts"
            :key="contact.id"
            class="contact-card"
            :class="`contact-${getEmergencyCategoryTone(contact.category)}`"
          >
            <div class="contact-card-heading">
              <span>{{ getEmergencyCategoryLabel(contact.category) }}</span>
              <Phone :size="18" />
            </div>
            <h4>{{ contact.displayName }}</h4>
            <strong class="contact-number">{{ contact.phoneNumber }}</strong>
            <p v-if="contact.note">{{ contact.note }}</p>
            <div class="source-row">
              <span>確認 {{ formatVerifiedDate(contact.verifiedOn) }}</span>
              <a :href="contact.sourceUrl" target="_blank" rel="noreferrer">
                {{ contact.sourceName }} <ArrowUpRight :size="13" />
              </a>
            </div>
          </article>
        </div>
      </section>

      <section v-if="emergencyStore.info.overseasOffice" class="office-panel">
        <div class="office-heading">
          <div class="office-icon"><Building2 :size="25" /></div>
          <div>
            <span class="eyebrow">TAIWAN OVERSEAS OFFICE</span>
            <h3>{{ emergencyStore.info.overseasOffice.nameZh }}</h3>
          </div>
        </div>

        <div class="office-grid">
          <div>
            <span>館址</span>
            <strong>{{ emergencyStore.info.overseasOffice.address }}</strong>
          </div>
          <div>
            <span>總機</span>
            <strong class="office-phone">{{ emergencyStore.info.overseasOffice.mainPhone }}</strong>
          </div>
          <div>
            <span>急難救助電話</span>
            <strong class="office-phone">{{ emergencyStore.info.overseasOffice.emergencyPhone }}</strong>
          </div>
        </div>
        <p v-if="emergencyStore.info.overseasOffice.note" class="office-note">
          {{ emergencyStore.info.overseasOffice.note }}
        </p>
        <div class="source-row office-source">
          <span>人工確認 {{ formatVerifiedDate(emergencyStore.info.overseasOffice.verifiedOn) }}</span>
          <a :href="emergencyStore.info.overseasOffice.sourceUrl" target="_blank" rel="noreferrer">
            {{ emergencyStore.info.overseasOffice.sourceName }}官方頁面 <ArrowUpRight :size="13" />
          </a>
        </div>
      </section>

      <section class="emergency-section">
        <div class="panel-heading-row">
          <div>
            <span class="eyebrow">WHAT TO DO</span>
            <h3>常見事故處理指引</h3>
          </div>
          <FileText :size="21" />
        </div>

        <div class="guide-list">
          <article v-for="guide in emergencyStore.info.guides" :key="guide.id" class="guide-card">
            <div class="guide-number">{{ String(emergencyStore.info.guides.indexOf(guide) + 1).padStart(2, '0') }}</div>
            <div class="guide-content">
              <h4>{{ guide.title }}</h4>
              <p>{{ guide.summary }}</p>
              <ol>
                <li v-for="step in guide.steps" :key="step">{{ step }}</li>
              </ol>
              <div class="source-row">
                <span>確認 {{ formatVerifiedDate(guide.verifiedOn) }}</span>
                <a :href="guide.sourceUrl" target="_blank" rel="noreferrer">
                  {{ guide.sourceName }} <ArrowUpRight :size="13" />
                </a>
              </div>
            </div>
          </article>
        </div>
      </section>

      <div class="data-source-footer emergency-source-footer">
        <Database :size="14" />
        <span>
          資料已存入系統主資料庫 · 最近人工確認
          {{ formatVerifiedDate(emergencyStore.info.lastVerifiedOn) }}
        </span>
      </div>
    </template>
  </div>
</template>

<style scoped>
.emergency-page { gap: 24px; }
.emergency-intro { background: linear-gradient(135deg, #fff 0%, #f6f2ff 100%); }
.safety-banner { display: flex; align-items: flex-start; gap: 13px; padding: 17px 20px; border: 1px solid #efce91; border-radius: 15px; background: #fff8e8; color: #8a5912; }
.safety-banner div { display: grid; gap: 3px; }
.safety-banner span { color: #775f3d; font-size: .86rem; line-height: 1.6; }
.state-panel { display: flex; min-height: 180px; align-items: center; justify-content: center; gap: 14px; padding: 28px; border: 1px dashed var(--line); border-radius: 22px; background: #fff; color: var(--muted); }
.error-panel { align-items: center; justify-content: flex-start; }
.error-panel strong { color: var(--navy); }
.error-panel p { margin: 4px 0 0; }
.error-panel .button { margin-left: auto; }
.emergency-section, .office-panel { padding: 26px; border: 1px solid var(--line); border-radius: 22px; background: #fff; }
.contact-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px; margin-top: 20px; }
.contact-card { --contact-color: #64748b; --contact-soft: #f1f5f9; display: flex; min-height: 230px; flex-direction: column; padding: 20px; border: 1px solid color-mix(in srgb, var(--contact-color) 25%, var(--line)); border-radius: 17px; background: linear-gradient(145deg, #fff 55%, var(--contact-soft)); }
.contact-card.contact-navy { --contact-color: #0f3b4b; --contact-soft: #e9f1f4; }
.contact-card.contact-coral { --contact-color: #b64732; --contact-soft: #fff0ec; }
.contact-card.contact-teal { --contact-color: #0f8078; --contact-soft: #e8f7f4; }
.contact-card.contact-violet { --contact-color: #7354a3; --contact-soft: #f3edfb; }
.contact-card-heading { display: flex; align-items: center; justify-content: space-between; color: var(--contact-color); font-size: .76rem; font-weight: 800; letter-spacing: .08em; }
.contact-card h4 { margin: 16px 0 5px; color: var(--navy); font-size: 1.05rem; }
.contact-number, .office-phone { color: var(--contact-color, var(--navy)); font-family: Georgia, 'Times New Roman', serif; font-size: 1.75rem; letter-spacing: .02em; }
.contact-card > p { flex: 1; margin: 14px 0; color: var(--muted); line-height: 1.65; }
.source-row { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding-top: 13px; border-top: 1px solid var(--line); color: var(--muted); font-size: .76rem; }
.source-row a { display: inline-flex; align-items: center; gap: 3px; color: var(--teal); font-weight: 800; text-align: right; text-decoration: none; }
.office-panel { background: linear-gradient(135deg, var(--navy) 0%, #164b5a 100%); color: #fff; }
.office-heading { display: flex; align-items: center; gap: 14px; }
.office-heading .eyebrow { color: #63d2c5; }
.office-heading h3 { margin: 5px 0 0; color: #fff; }
.office-icon { display: grid; width: 50px; height: 50px; place-items: center; border-radius: 15px; background: rgba(255,255,255,.1); color: #74d9cf; }
.office-grid { display: grid; grid-template-columns: 1.35fr .85fr 1.2fr; gap: 12px; margin-top: 22px; }
.office-grid > div { display: grid; gap: 8px; padding: 16px; border-radius: 14px; background: rgba(255,255,255,.08); }
.office-grid span { color: #9ac1ca; font-size: .76rem; }
.office-grid strong { color: #fff; line-height: 1.55; }
.office-grid .office-phone { font-size: 1.18rem; }
.office-note { margin: 16px 0 0; color: #c9dce1; line-height: 1.7; }
.office-source { margin-top: 18px; border-top-color: rgba(255,255,255,.16); color: #a9c9d1; }
.office-source a { color: #73ddd1; }
.guide-list { display: grid; gap: 15px; margin-top: 20px; }
.guide-card { display: grid; grid-template-columns: 56px 1fr; gap: 18px; padding: 21px; border: 1px solid var(--line); border-radius: 17px; background: var(--canvas); }
.guide-number { color: #a9bdc3; font-family: Georgia, 'Times New Roman', serif; font-size: 1.55rem; }
.guide-content h4 { margin: 0; color: var(--navy); font-size: 1.1rem; }
.guide-content > p { margin: 8px 0 12px; color: var(--muted); line-height: 1.65; }
.guide-content ol { display: grid; gap: 8px; margin: 0 0 17px; padding-left: 1.35rem; color: var(--ink); line-height: 1.6; }
.emergency-source-footer { justify-content: center; }
@media (max-width: 760px) {
  .emergency-section, .office-panel { padding: 20px; }
  .contact-grid, .office-grid { grid-template-columns: 1fr; }
  .contact-card { min-height: auto; }
  .guide-card { grid-template-columns: 1fr; gap: 8px; }
  .error-panel { flex-wrap: wrap; }
  .error-panel .button { width: 100%; margin-left: 0; }
  .source-row { align-items: flex-start; }
}
</style>
