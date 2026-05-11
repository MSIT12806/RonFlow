<template>
  <div
    class="base-state-block base-state-block-loading"
    data-testid="base-loading-state"
    role="status"
    aria-live="polite"
  >
    <p class="base-state-message">{{ message }}</p>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  message: string
}>()
</script>