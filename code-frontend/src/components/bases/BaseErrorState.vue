<template>
  <div
    class="base-state-block base-state-block-error"
    :class="{ 'base-state-block-page': scope === 'page' }"
    data-testid="base-error-state"
    role="alert"
  >
    <p class="base-state-message">{{ message }}</p>
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  message: string
  scope?: 'page' | 'section'
}>(), {
  scope: 'section',
})
</script>