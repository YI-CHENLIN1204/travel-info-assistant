<script setup lang="ts">
import { computed, ref, watch, type Component } from 'vue'
import {
  Cloud,
  CloudFog,
  CloudLightning,
  CloudRain,
  CloudSnow,
  CloudSun,
  Database,
  Droplets,
  LocateFixed,
  RefreshCw,
  Sun,
  ThermometerSun,
  Umbrella,
  Wind,
} from '@lucide/vue'
import StatusPill from '@/components/StatusPill.vue'
import { getWeatherIconKind, getWindDirection, type WeatherIconKind } from '@/services/weatherDisplay'
import { useCityStore } from '@/stores/city'
import { useWeatherStore } from '@/stores/weather'

const cityStore = useCityStore()
const weatherStore = useWeatherStore()
const locationMessage = ref<string | null>(null)

const iconComponents: Record<WeatherIconKind, Component> = {
  clear: Sun,
  'partly-cloudy': CloudSun,
  cloudy: Cloud,
  rain: CloudRain,
  snow: CloudSnow,
  thunder: CloudLightning,
  fog: CloudFog,
}

const weather = computed(() => weatherStore.forecast)
const currentIcon = computed(() => weatherIcon(weather.value?.current.conditionCode ?? 'clear'))
const sourceUpdatedAt = computed(
  () => weatherStore.resultMeta?.sourceUpdatedAt ?? weatherStore.resultMeta?.fetchedAt ?? null,
)

watch(
  () => cityStore.selectedCityId,
  (cityId) => {
    if (!cityId) return
    locationMessage.value = null
    void weatherStore.loadCity(cityId)
  },
  { immediate: true },
)

async function useCurrentLocation(): Promise<void> {
  locationMessage.value = null
  if (!cityStore.currentCoordinates) {
    await cityStore.requestLocation()
  }

  const coordinates = cityStore.currentCoordinates
  if (!coordinates) {
    locationMessage.value = cityStore.locationStatus === 'denied'
      ? '瀏覽器尚未開啟定位權限，先顯示目前選擇城市的天氣。'
      : '目前無法取得定位，先顯示目前選擇城市的天氣。'
    return
  }

  await weatherStore.loadLocation(coordinates.latitude, coordinates.longitude)
}

function weatherIcon(conditionCode: string): Component {
  return iconComponents[getWeatherIconKind(conditionCode)]
}

function temperature(value: number | null): string {
  return value === null ? '—' : `${Math.round(value)}°`
}

function measurement(value: number | null, unit: string): string {
  return value === null ? '尚未提供' : `${value}${unit}`
}

function formatHour(value: string): string {
  return new Intl.DateTimeFormat('zh-TW', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: weather.value?.timeZone,
  }).format(new Date(value))
}

function formatDay(value: string, index: number): string {
  if (index === 0) return '今天'
  return new Intl.DateTimeFormat('zh-TW', {
    month: 'numeric',
    day: 'numeric',
    weekday: 'short',
    timeZone: weather.value?.timeZone,
  }).format(new Date(`${value}T12:00:00Z`))
}

function formatUpdatedAt(value: string | null): string {
  if (!value) return '尚未提供'
  return new Intl.DateTimeFormat('zh-TW', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: weather.value?.timeZone,
  }).format(new Date(value))
}
</script>

<template>
  <div class="content-stack weather-page">
    <section class="page-intro weather-intro">
      <div>
        <StatusPill tone="ready" label="全球天氣已啟用" />
        <h2>{{ cityStore.currentCity.nameZh }}天氣</h2>
        <p>查看目前城市或定位地點的即時預報摘要；資料由後端共用快取，不會因重整頁面重複呼叫來源。</p>
        <button
          class="button button-ghost weather-location-button"
          type="button"
          :disabled="weatherStore.loading || cityStore.locationStatus === 'requesting'"
          @click="useCurrentLocation"
        >
          <LocateFixed :size="17" />
          使用目前位置
        </button>
      </div>
      <div class="intro-icon"><CloudSun :size="34" /></div>
    </section>

    <div
      v-if="locationMessage"
      class="provider-message provider-message-warning"
      role="status"
    >
      <LocateFixed :size="18" />
      <span>{{ locationMessage }}</span>
    </div>

    <div
      v-if="weatherStore.resultMeta?.message && weather"
      class="provider-message provider-message-warning"
      role="status"
    >
      <RefreshCw :size="18" />
      <span>{{ weatherStore.resultMeta.message }}</span>
    </div>

    <section v-if="weather" class="weather-current-panel">
      <div class="weather-current-main">
        <div class="weather-current-heading">
          <div>
            <span class="eyebrow">CURRENT FORECAST</span>
            <h3>{{ weather.locationName }}</h3>
          </div>
          <span class="weather-scope-label">
            {{ weatherStore.scope === 'location' ? '目前位置' : '城市中心' }}
          </span>
        </div>

        <div class="weather-current-reading">
          <span class="weather-current-icon">
            <component :is="currentIcon" :size="54" stroke-width="1.5" />
          </span>
          <div>
            <strong>{{ temperature(weather.current.temperatureCelsius) }}</strong>
            <span>{{ weather.current.conditionLabel }}</span>
            <small>體感 {{ temperature(weather.current.apparentTemperatureCelsius) }}</small>
          </div>
        </div>
      </div>

      <div class="weather-metrics">
        <article>
          <Droplets :size="20" />
          <span>濕度</span>
          <strong>{{ measurement(weather.current.humidityPercent, '%') }}</strong>
        </article>
        <article>
          <Umbrella :size="20" />
          <span>降雨機率</span>
          <strong>{{ measurement(weather.current.precipitationProbabilityPercent, '%') }}</strong>
        </article>
        <article>
          <CloudRain :size="20" />
          <span>預估降雨</span>
          <strong>{{ measurement(weather.current.precipitationMillimeters, ' mm') }}</strong>
        </article>
        <article>
          <Wind :size="20" />
          <span>{{ getWindDirection(weather.current.windFromDirectionDegrees) }}</span>
          <strong>{{ measurement(weather.current.windSpeedMetersPerSecond, ' m/s') }}</strong>
        </article>
      </div>

      <div class="data-source-footer weather-source-footer">
        <Database :size="14" />
        <span>
          資料來源
          <a href="https://api.met.no/" target="_blank" rel="noreferrer">MET Norway</a>
          · 更新 {{ formatUpdatedAt(sourceUpdatedAt) }}
        </span>
        <span v-if="weatherStore.resultMeta?.stale" class="stale-badge">備援快取</span>
      </div>
    </section>

    <section v-if="weather" class="weather-forecast-panel">
      <div class="panel-heading-row">
        <div>
          <span class="eyebrow">NEXT HOURS</span>
          <h3>未來時段</h3>
        </div>
        <span>當地時間</span>
      </div>
      <div class="weather-hourly-list">
        <article v-for="hour in weather.hourly" :key="hour.time" class="weather-hour-card">
          <span>{{ formatHour(hour.time) }}</span>
          <component :is="weatherIcon(hour.conditionCode)" :size="25" />
          <strong>{{ temperature(hour.temperatureCelsius) }}</strong>
          <small>
            <Umbrella :size="12" />
            {{ measurement(hour.precipitationProbabilityPercent, '%') }}
          </small>
        </article>
      </div>
    </section>

    <section v-if="weather" class="weather-forecast-panel">
      <div class="panel-heading-row">
        <div>
          <span class="eyebrow">DAILY FORECAST</span>
          <h3>未來幾天</h3>
        </div>
        <ThermometerSun :size="20" />
      </div>
      <div class="weather-daily-list">
        <article v-for="(day, index) in weather.daily" :key="day.date" class="weather-day-row">
          <strong>{{ formatDay(day.date, index) }}</strong>
          <span class="weather-day-condition">
            <component :is="weatherIcon(day.conditionCode)" :size="22" />
            {{ day.conditionLabel }}
          </span>
          <span class="weather-day-rain">
            <Umbrella :size="14" />
            {{ measurement(day.precipitationProbabilityPercent, '%') }}
          </span>
          <span class="weather-day-temperature">
            <b>{{ temperature(day.maximumTemperatureCelsius) }}</b>
            <small>{{ temperature(day.minimumTemperatureCelsius) }}</small>
          </span>
        </article>
      </div>
    </section>

    <section v-else-if="weatherStore.loading" class="empty-state compact-empty" aria-live="polite">
      <span class="empty-state-icon"><RefreshCw :size="32" /></span>
      <h3>正在取得天氣資料</h3>
      <p>系統正在讀取 MET Norway 預報與共用快取。</p>
    </section>

    <section v-else class="empty-state compact-empty" aria-live="polite">
      <span class="empty-state-icon"><CloudSun :size="32" /></span>
      <h3>目前無法顯示天氣</h3>
      <p>{{ weatherStore.error ?? '請稍後再試。' }}</p>
      <button
        v-if="cityStore.selectedCityId"
        class="button button-primary weather-retry-button"
        type="button"
        @click="weatherStore.loadCity(cityStore.selectedCityId)"
      >
        <RefreshCw :size="16" />
        重新整理
      </button>
    </section>
  </div>
</template>
