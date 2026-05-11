<template>
  <div class="command-resource-shell" data-testid="api-command-resource-view">
    <slot />

    <p
      v-if="isSubmitting"
      class="command-resource-feedback command-resource-feedback-submitting"
      data-testid="api-command-submitting"
      role="status"
      aria-live="polite"
    >
      {{ submittingMessage }}
    </p>

    <p
      v-else-if="errorMessage"
      class="command-resource-feedback command-resource-feedback-error"
      data-testid="api-command-error"
      role="alert"
    >
      {{ errorMessage }}
    </p>
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  isSubmitting: boolean
  errorMessage: string
  submittingMessage?: string
}>(), {
  submittingMessage: '處理中，請稍候...',
})
</script>