<template>
  <BaseModalShell
    :is-open="isOpen"
    title="任務詳細資訊"
    title-id="task-detail-title"
    eyebrow="Task detail"
    size="wide"
    @close="$emit('close')"
  >
      <AsyncStateBoundary
        :is-loading="isLoading"
        :error-message="errorMessage"
        loading-message="正在載入任務詳細資訊..."
      >
        <section v-if="task" class="detail-layout">
          <div class="detail-card">
            <label class="detail-label" for="task-detail-title-input">任務標題</label>
            <input
              id="task-detail-title-input"
              v-model="draftTitle"
              type="text"
              :disabled="isSaving"
            />
            <p v-if="titleValidationError" class="error-copy">{{ titleValidationError }}</p>
          </div>

          <div class="detail-card detail-card-full">
            <label class="detail-label" for="task-detail-description-input">任務描述</label>
            <textarea
              id="task-detail-description-input"
              v-model="draftDescription"
              rows="4"
              :disabled="isSaving"
            ></textarea>
          </div>

          <div class="detail-card">
            <p class="detail-label">狀態</p>
            <strong>{{ task.currentState.label }}</strong>
          </div>

          <div class="detail-card">
            <label class="detail-label" for="task-detail-due-date-input">到期日</label>
            <input
              id="task-detail-due-date-input"
              v-model="draftDueDate"
              type="date"
              :disabled="isSaving"
            />
          </div>

          <div class="detail-card">
            <p class="detail-label">建立時間</p>
            <strong>{{ formatTimelineTime(task.createdAt) }}</strong>
          </div>

          <div v-if="task.completedAt" class="detail-card">
            <p class="detail-label">完成時間</p>
            <strong>{{ formatTimelineTime(task.completedAt) }}</strong>
          </div>

          <div class="detail-card detail-card-full">
            <ApiCommandResourceView
              :is-submitting="isSaving"
              :error-message="saveErrorMessage"
              submitting-message="正在儲存任務變更，請稍候..."
            />

            <div class="modal-actions">
              <button type="button" class="primary-button" :disabled="isSaving" @click="submit">儲存變更</button>
            </div>
          </div>

          <div class="detail-card detail-card-full">
            <p class="detail-label">活動紀錄</p>
            <ul class="history-list">
              <li v-for="entry in task.activityTimeline" :key="`${entry.type}-${entry.occurredAt}`">
                <span>{{ entry.message }}</span>
                <small>{{ formatTimelineTime(entry.occurredAt) }}</small>
              </li>
            </ul>
          </div>
        </section>
      </AsyncStateBoundary>
  </BaseModalShell>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import AsyncStateBoundary from './bases/AsyncStateBoundary.vue'
import ApiCommandResourceView from './bases/ApiCommandResourceView.vue'
import BaseModalShell from './bases/BaseModalShell.vue'
import type { TaskDetailResponse } from '../api/ronflowApi'

const props = defineProps<{
  isOpen: boolean
  isLoading: boolean
  isSaving: boolean
  errorMessage: string
  saveErrorMessage: string
  titleValidationError: string
  task: TaskDetailResponse | null
  formatTimelineTime: (occurredAt: string) => string
}>()

const emit = defineEmits<{
  (event: 'close'): void
  (event: 'save', payload: { taskId: string; title: string; description: string; dueDate: string | null }): void
}>()

const draftTitle = ref('')
const draftDescription = ref('')
const draftDueDate = ref('')

function submit() {
  if (!props.task || props.isSaving) {
    return
  }

  emit('save', {
    taskId: props.task.id,
    title: draftTitle.value,
    description: draftDescription.value,
    dueDate: draftDueDate.value || null,
  })
}

watch(
  () => [props.isOpen, props.task?.id, props.task?.title, props.task?.description, props.task?.dueDate] as const,
  ([isOpen]) => {
    if (!isOpen || !props.task) {
      return
    }

    draftTitle.value = props.task.title
    draftDescription.value = props.task.description
    draftDueDate.value = props.task.dueDate ?? ''
  },
  { immediate: true },
)
</script>