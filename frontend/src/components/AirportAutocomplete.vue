<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue'
import { MapPin, Search } from '@lucide/vue'
import { searchAirports } from '@/api/flights'
import type { Airport } from '@/types/api'

const props = defineProps<{
  modelValue: Airport | null
  label: string
  placeholder: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: Airport | null]
}>()

const query = ref('')
const options = ref<Airport[]>([])
const open = ref(false)
const loading = ref(false)
let timer: number | undefined
let requestSequence = 0

watch(
  () => props.modelValue,
  (airport) => {
    query.value = airport ? formatAirport(airport) : ''
  },
  { immediate: true },
)

onUnmounted(() => {
  if (timer !== undefined) window.clearTimeout(timer)
})

function onInput(): void {
  if (props.modelValue && query.value !== formatAirport(props.modelValue)) {
    emit('update:modelValue', null)
  }

  if (timer !== undefined) window.clearTimeout(timer)
  const term = query.value.trim()
  if (!term) {
    options.value = []
    open.value = false
    return
  }

  timer = window.setTimeout(() => void loadOptions(term), 220)
}

async function loadOptions(term: string): Promise<void> {
  const sequence = ++requestSequence
  loading.value = true
  try {
    const response = await searchAirports(term)
    if (sequence !== requestSequence) return
    options.value = response.data
    open.value = true
  } catch {
    if (sequence !== requestSequence) return
    options.value = []
    open.value = true
  } finally {
    if (sequence === requestSequence) loading.value = false
  }
}

function choose(airport: Airport): void {
  emit('update:modelValue', airport)
  query.value = formatAirport(airport)
  options.value = []
  open.value = false
}

function formatAirport(airport: Airport): string {
  return `${airport.iata} · ${airport.municipality ?? airport.name}`
}
</script>

<template>
  <label class="airport-field">
    <span>{{ label }}</span>
    <span class="input-shell">
      <Search :size="17" />
      <input
        v-model="query"
        :placeholder="placeholder"
        autocomplete="off"
        role="combobox"
        aria-autocomplete="list"
        :aria-expanded="open"
        @focus="query.trim() && (open = true)"
        @input="onInput"
        @keydown.escape="open = false"
      />
    </span>
    <span v-if="open" class="airport-options" role="listbox">
      <button
        v-for="airport in options"
        :key="airport.iata"
        type="button"
        role="option"
        @mousedown.prevent="choose(airport)"
      >
        <strong>{{ airport.iata }}</strong>
        <span>
          {{ airport.municipality ?? airport.name }}
          <small>{{ airport.name }}</small>
        </span>
        <MapPin :size="15" />
      </button>
      <span v-if="loading" class="airport-options-state">搜尋機場中…</span>
      <span v-else-if="!options.length" class="airport-options-state">找不到符合的定期航班機場</span>
    </span>
  </label>
</template>
