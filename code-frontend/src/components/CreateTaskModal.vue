<template>
  <div v-if="isOpen" class="modal-backdrop">
    <div role="dialog" aria-modal="true" aria-labelledby="create-task-title" class="modal-card">
      <div class="modal-header">
        <div>
          <p class="eyebrow">New task</p>
          <h2 id="create-task-title">建立任務</h2>
        </div>
        <button type="button" class="ghost-icon-button" aria-label="關閉視窗" @click="$emit('close')">
          ×
        </button>
      </div>

      <form class="modal-form" @submit.prevent="$emit('submit')">
        <label for="task-title">任務標題</label>
        <input
          id="task-title"
          :value="taskTitle"
          type="text"
          autocomplete="off"
          @input="$emit('update:task-title', ($event.target as HTMLInputElement).value)"
        />
        <p v-if="taskTitleError" class="error-copy">{{ taskTitleError }}</p>

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
  taskTitle: string
  taskTitleError: string
  isSubmitting: boolean
}>()

defineEmits<{
  (event: 'close'): void
  (event: 'submit'): void
  (event: 'update:task-title', value: string): void
}>()
</script>