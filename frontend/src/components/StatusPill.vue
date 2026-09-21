<script setup lang="ts">
defineProps<{
  tone: 'ready' | 'planned' | 'warning' | 'neutral'
  label: string
}>()
</script>

<template>
  <span class="status-pill" :class="`status-pill-${tone}`">{{ label }}</span>
</template>
