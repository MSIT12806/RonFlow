<template>
  <div v-if="isOpen" class="modal-backdrop">
    <div role="dialog" aria-modal="true" aria-labelledby="create-project-title" class="modal-card">
      <div class="modal-header">
        <div>
          <p class="eyebrow">New project</p>
          <h2 id="create-project-title">建立專案</h2>
        </div>
        <button type="button" class="ghost-icon-button" aria-label="關閉視窗" @click="$emit('close')">
          ×
        </button>
      </div>

      <form class="modal-form" @submit.prevent="$emit('submit')">
        <label for="project-name">專案名稱</label>
        <input
          id="project-name"
          :value="projectName"
          type="text"
          autocomplete="off"
          @input="$emit('update:project-name', ($event.target as HTMLInputElement).value)"
        />
        <p v-if="projectNameError" class="error-copy">{{ projectNameError }}</p>

        <div class="modal-actions">
          <button type="button" class="secondary-button" :disabled="isSubmitting" @click="$emit('close')">取消</button>
          <button type="submit" class="primary-button" :disabled="isSubmitting">建立</button>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  isOpen: boolean
  projectName: string
  projectNameError: string
  isSubmitting: boolean
}>()

defineEmits<{
  (event: 'close'): void
  (event: 'submit'): void
  (event: 'update:project-name', value: string): void
}>()
</script>