<template>
  <AsyncStateBoundary v-bind="props">
    <slot />
  </AsyncStateBoundary>
</template>

<script setup lang="ts">
import AsyncStateBoundary from './AsyncStateBoundary.vue'

const props = withDefaults(defineProps<{
  isLoading: boolean
  errorMessage: string
  loadingMessage?: string
  errorScope?: 'page' | 'section'
}>(), {
  loadingMessage: '載入中...',
  errorScope: 'section',
})
</script>