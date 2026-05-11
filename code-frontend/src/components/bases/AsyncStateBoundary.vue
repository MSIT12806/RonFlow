<template>
  <BaseLoadingState
    v-if="isLoading"
    :message="loadingMessage"
  />

  <BaseErrorState
    v-else-if="errorMessage"
    :message="errorMessage"
    :scope="errorScope"
  />

  <slot v-else />
</template>

<script setup lang="ts">
import BaseErrorState from './BaseErrorState.vue'
import BaseLoadingState from './BaseLoadingState.vue'

withDefaults(defineProps<{
  isLoading: boolean
  errorMessage: string
  loadingMessage?: string
  errorScope?: 'page' | 'section'
}>(), {
  loadingMessage: '載入中...',
  errorScope: 'section',
})
</script>