<template>
  <div v-if="isOpen" data-testid="base-modal-shell" class="base-modal-shell__backdrop">
    <div
      :class="['base-modal-shell__card', { 'base-modal-shell__card--wide': size === 'wide' }]"
      data-testid="base-modal-shell-card"
      role="dialog"
      aria-modal="true"
      :aria-labelledby="titleId"
    >
      <div class="base-modal-shell__header">
        <div>
          <p v-if="eyebrow" class="eyebrow">{{ eyebrow }}</p>
          <h2 :id="titleId" class="base-modal-shell__title">{{ title }}</h2>
        </div>
        <button
          type="button"
          class="ghost-icon-button"
          aria-label="關閉視窗"
          :disabled="closeDisabled"
          @click="$emit('close')"
        >
          ×
        </button>
      </div>

      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  isOpen: boolean
  title: string
  titleId: string
  eyebrow?: string
  size?: 'default' | 'wide'
  closeDisabled?: boolean
}>(), {
  eyebrow: '',
  size: 'default',
  closeDisabled: false,
})

defineEmits<{
  (event: 'close'): void
}>()
</script>

<style scoped>
.base-modal-shell__backdrop {
  position: fixed;
  inset: 0;
  z-index: 30;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  overflow-y: auto;
  background: rgba(15, 23, 42, 0.34);
}

.base-modal-shell__card {
  width: min(560px, 100%);
  max-height: calc(100dvh - 48px);
  padding: 24px;
  overflow-y: auto;
  border: 1px solid rgba(255, 255, 255, 0.75);
  border-radius: 28px;
  background: rgba(255, 252, 248, 0.94);
}

.base-modal-shell__card--wide {
  width: min(720px, 100%);
}

.base-modal-shell__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.base-modal-shell__title {
  margin: 0;
}

@media (max-width: 720px) {
  .base-modal-shell__header {
    flex-direction: column;
    align-items: stretch;
  }

  .base-modal-shell__card {
    padding: 20px;
  }
}
</style>