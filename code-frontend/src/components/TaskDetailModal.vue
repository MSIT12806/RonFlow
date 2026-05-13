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
            <div class="detail-field">
              <label class="detail-label" for="task-detail-title-input">任務標題</label>

              <div class="detail-field-control">
                <InputText
                  id="task-detail-title-input"
                  v-model="draftTitle"
                  fluid
                  :disabled="isSaving"
                  :invalid="Boolean(titleValidationError)"
                />
                <p v-if="titleValidationError" class="error-copy">{{ titleValidationError }}</p>
              </div>
            </div>
          </div>

          <div class="detail-card detail-card-full">
            <div class="detail-field detail-field-inline">
              <label class="detail-label" for="task-detail-description-input">任務描述</label>

              <div class="detail-field-control">
                <Textarea
                  id="task-detail-description-input"
                  v-model="draftDescription"
                  fluid
                  auto-resize
                  rows="5"
                  :disabled="isSaving"
                />
              </div>
            </div>
          </div>

          <div class="detail-card">
            <p class="detail-label">狀態</p>
            <strong>{{ task.currentState.label }}</strong>
          </div>

          <div class="detail-card">
            <div class="detail-field">
              <label class="detail-label" for="task-detail-due-date-input">到期日</label>

              <div class="detail-field-control">
                <DatePicker
                  input-id="task-detail-due-date-input"
                  v-model="draftDueDateValue"
                  fluid
                  date-format="yy-mm-dd"
                  icon-display="input"
                  show-button-bar
                  show-clear
                  show-icon
                  :disabled="isSaving"
                  :manual-input="false"
                />
              </div>
            </div>
          </div>

          <div class="detail-card">
            <p class="detail-label">建立時間</p>
            <strong>{{ formatTimelineTime(task.createdAt) }}</strong>
          </div>

          <div v-if="task.currentState.isCompletedState && task.completedAt" class="detail-card">
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
import { computed, ref, watch } from 'vue'
import DatePicker from 'primevue/datepicker'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
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

const draftDueDateValue = computed<Date | null>({
  get() {
    return parseDateOnly(draftDueDate.value)
  },
  set(value) {
    draftDueDate.value = formatDateOnly(value)
  },
})

function parseDateOnly(value: string): Date | null {
  if (!value) {
    return null
  }

  const [yearText, monthText, dayText] = value.split('-')
  const year = Number(yearText)
  const month = Number(monthText)
  const day = Number(dayText)

  if (!year || !month || !day) {
    return null
  }

  return new Date(year, month - 1, day)
}

function formatDateOnly(value: Date | null): string {
  if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
    return ''
  }

  const year = value.getFullYear()
  const month = String(value.getMonth() + 1).padStart(2, '0')
  const day = String(value.getDate()).padStart(2, '0')

  return `${year}-${month}-${day}`
}

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