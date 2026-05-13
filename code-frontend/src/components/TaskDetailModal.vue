<template>
  <BaseModalShell
    :is-open="isOpen"
    :close-disabled="isSaving"
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
          <div class="detail-card detail-card-full detail-toolbar">
            <div v-if="isReadOnly" class="detail-lifecycle-banner">
              <strong v-if="mode === 'archived'">此任務已封存</strong>
              <strong v-else>此任務位於垃圾桶</strong>
            </div>

            <div v-else class="detail-toolbar-actions">
              <button
                type="button"
                class="secondary-button"
                aria-haspopup="menu"
                :aria-expanded="isActionsOpen"
                @click="toggleActionsMenu"
              >
                更多操作
              </button>

              <div v-if="isActionsOpen" class="detail-actions-menu" role="menu">
                <button type="button" class="detail-actions-menu-item" role="menuitem" @click="emitArchive">
                  封存
                </button>
                <button type="button" class="detail-actions-menu-item" role="menuitem" @click="emitMoveToTrash">
                  移到垃圾桶
                </button>
              </div>
            </div>
          </div>

          <div class="detail-card">
            <div class="detail-field">
              <label class="detail-label" for="task-detail-title-input">任務標題</label>

              <div class="detail-field-control">
                <InputText
                  id="task-detail-title-input"
                  v-model="draftTitle"
                  fluid
                  :disabled="isSaving || isReadOnly"
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
                  :disabled="isSaving || isReadOnly"
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
                  :disabled="isSaving || isReadOnly"
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
              submitting-message="正在提交任務操作，請稍候..."
            />

            <div class="modal-actions">
              <button v-if="!isReadOnly" type="button" class="primary-button" :disabled="isSaving" @click="submit">儲存變更</button>
              <button v-else type="button" class="primary-button" :disabled="isSaving" @click="emitRestore">還原</button>
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
import type { TaskDetailMode } from '../composables/useRonFlowBoard'

const props = defineProps<{
  isOpen: boolean
  isLoading: boolean
  isSaving: boolean
  errorMessage: string
  saveErrorMessage: string
  titleValidationError: string
  mode: TaskDetailMode
  task: TaskDetailResponse | null
  formatTimelineTime: (occurredAt: string) => string
}>()

const emit = defineEmits<{
  (event: 'close'): void
  (event: 'save', payload: { taskId: string; title: string; description: string; dueDate: string | null }): void
  (event: 'archive', taskId: string): void
  (event: 'move-to-trash', taskId: string): void
  (event: 'restore', taskId: string, mode: Exclude<TaskDetailMode, 'active'>): void
}>()

const draftTitle = ref('')
const draftDescription = ref('')
const draftDueDate = ref('')
const isActionsOpen = ref(false)

const isReadOnly = computed(() => props.mode !== 'active')

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
  if (!props.task || props.isSaving || isReadOnly.value) {
    return
  }

  emit('save', {
    taskId: props.task.id,
    title: draftTitle.value,
    description: draftDescription.value,
    dueDate: draftDueDate.value || null,
  })
}

function toggleActionsMenu() {
  if (props.isSaving || isReadOnly.value) {
    return
  }

  isActionsOpen.value = !isActionsOpen.value
}

function emitArchive() {
  if (!props.task || props.isSaving) {
    return
  }

  isActionsOpen.value = false
  emit('archive', props.task.id)
}

function emitMoveToTrash() {
  if (!props.task || props.isSaving) {
    return
  }

  isActionsOpen.value = false
  emit('move-to-trash', props.task.id)
}

function emitRestore() {
  if (!props.task || props.isSaving || props.mode === 'active') {
    return
  }

  emit('restore', props.task.id, props.mode)
}

watch(
  () => [props.isOpen, props.task?.id, props.task?.title, props.task?.description, props.task?.dueDate, props.mode] as const,
  ([isOpen]) => {
    isActionsOpen.value = false

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