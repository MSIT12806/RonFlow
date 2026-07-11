<template>
  <div
    v-if="isOpen"
    data-testid="base-modal-shell"
    :class="[
      'base-modal-shell__backdrop',
      `base-modal-shell__backdrop--${presentation}`,
      { 'base-modal-shell__backdrop--allow-underlay-interaction': allowUnderlayInteraction },
    ]"
  >
    <div
      ref="cardRef"
      :class="[
        'base-modal-shell__card',
        `base-modal-shell__card--${presentation}`,
        { 'base-modal-shell__card--wide': size === 'wide' },
      ]"
      data-testid="base-modal-shell-card"
      role="dialog"
      :aria-modal="allowUnderlayInteraction ? 'false' : 'true'"
      :aria-labelledby="titleId"
    >
      <div class="base-modal-shell__header">
        <div class="base-modal-shell__header-copy">
          <p v-if="eyebrow" class="eyebrow">{{ eyebrow }}</p>
          <h2 :id="titleId" class="base-modal-shell__title">{{ title }}</h2>
        </div>
        <div class="base-modal-shell__header-actions">
          <slot name="header-actions" />
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
      </div>

      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'

const props = withDefaults(defineProps<{
  isOpen: boolean
  title: string
  titleId: string
  eyebrow?: string
  size?: 'default' | 'wide'
  presentation?: 'modal' | 'drawer'
  closeDisabled?: boolean
  allowUnderlayInteraction?: boolean
  closeOnEscape?: boolean
  closeOnInteractOutside?: boolean
}>(), {
  eyebrow: '',
  size: 'default',
  presentation: 'modal',
  closeDisabled: false,
  allowUnderlayInteraction: false,
  closeOnEscape: false,
  closeOnInteractOutside: false,
})

const emit = defineEmits<{
  (event: 'close'): void
}>()

const cardRef = ref<HTMLElement | null>(null)
let pendingOutsideListenerTimer: ReturnType<typeof window.setTimeout> | null = null

function handleDocumentClick(event: MouseEvent) {
  if (!props.isOpen || props.closeDisabled || !props.closeOnInteractOutside) {
    return
  }

  const target = event.target
  const card = cardRef.value
  if (!(target instanceof Node) || card === null || card.contains(target)) {
    return
  }

  const dialogAncestor = target instanceof Element ? target.closest('[role="dialog"]') : null
  if (dialogAncestor && dialogAncestor !== card) {
    return
  }

  emit('close')
}

function handleWindowKeydown(event: KeyboardEvent) {
  if (!props.isOpen || props.closeDisabled || !props.closeOnEscape || event.key !== 'Escape') {
    return
  }

  emit('close')
}

function stopGlobalListeners() {
  if (pendingOutsideListenerTimer !== null) {
    window.clearTimeout(pendingOutsideListenerTimer)
    pendingOutsideListenerTimer = null
  }

  document.removeEventListener('click', handleDocumentClick)
  window.removeEventListener('keydown', handleWindowKeydown)
}

function syncGlobalListeners() {
  stopGlobalListeners()

  if (!props.isOpen) {
    return
  }

  if (props.closeOnInteractOutside) {
    pendingOutsideListenerTimer = window.setTimeout(() => {
      document.addEventListener('click', handleDocumentClick)
      pendingOutsideListenerTimer = null
    }, 0)
  }

  if (props.closeOnEscape) {
    window.addEventListener('keydown', handleWindowKeydown)
  }
}

watch(
  () => [props.isOpen, props.closeOnEscape, props.closeOnInteractOutside],
  syncGlobalListeners,
  { immediate: true },
)

onBeforeUnmount(() => {
  stopGlobalListeners()
})
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

.base-modal-shell__backdrop--drawer {
  align-items: stretch;
  justify-content: flex-end;
  padding: 16px;
  background: transparent;
}

.base-modal-shell__backdrop--allow-underlay-interaction {
  pointer-events: none;
}

.base-modal-shell__card {
  width: min(560px, 100%);
  max-height: calc(100dvh - 48px);
  padding: 24px;
  overflow-y: auto;
  border: 1px solid rgba(255, 255, 255, 0.75);
  border-radius: 28px;
  background: rgba(255, 252, 248, 0.94);
  pointer-events: auto;
}

.base-modal-shell__card--wide {
  width: min(720px, 100%);
}

.base-modal-shell__card--drawer {
  width: min(640px, 100%);
  max-height: calc(100dvh - 32px);
  border-radius: 24px;
  box-shadow: 0 28px 72px rgba(15, 23, 42, 0.18);
}

.base-modal-shell__card--drawer.base-modal-shell__card--wide {
  width: min(680px, 100%);
}

.base-modal-shell__header {
  position: sticky;
  top: -24px;
  z-index: 8;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin: -24px -24px 0;
  padding: 24px 24px 16px;
  background: rgba(255, 252, 248, 0.96);
  backdrop-filter: blur(14px);
}

.base-modal-shell__header-copy {
  min-width: 0;
}

.base-modal-shell__header-actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 12px;
  flex-wrap: wrap;
}

.base-modal-shell__title {
  margin: 0;
}

@media (max-width: 720px) {
  .base-modal-shell__backdrop--drawer {
    padding: 0;
  }

  .base-modal-shell__header {
    flex-direction: column;
    align-items: stretch;
    top: -20px;
    margin: -20px -20px 0;
    padding: 20px 20px 14px;
  }

  .base-modal-shell__card {
    padding: 20px;
  }

  .base-modal-shell__card--drawer {
    width: 100%;
    max-height: 100dvh;
    border-radius: 0;
  }
}
</style>
