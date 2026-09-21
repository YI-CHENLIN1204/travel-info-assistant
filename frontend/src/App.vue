<script setup lang="ts">
import { onMounted } from 'vue'
import AppShell from '@/components/AppShell.vue'
import { useAppStore } from '@/stores/app'
import { useCityStore } from '@/stores/city'

const appStore = useAppStore()
const cityStore = useCityStore()

onMounted(async () => {
  await Promise.all([appStore.initialize(), cityStore.initialize()])
  void cityStore.requestLocation()
})
</script>

<template>
  <AppShell />
</template>
