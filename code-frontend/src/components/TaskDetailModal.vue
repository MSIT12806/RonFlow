<template>
  <BaseModalShell
    :is-open="isOpen"
    :close-disabled="isSaving"
    allow-underlay-interaction
    title="任務詳細資訊"
    title-id="task-detail-title"
    eyebrow="Task detail"
    size="wide"
    @close="$emit('close')"
  >
      <section v-if="displayTitle" class="detail-preview-header">
        <h3>{{ displayTitle }}</h3>
      </section>

      <AsyncStateBoundary
        :is-loading="isLoading"
        :error-message="errorMessage"
        loading-message="正在載入任務詳細資訊..."
      >
        <section v-if="task" class="detail-layout">
          <div class="detail-card detail-card-full detail-toolbar">
            <div v-if="isLifecycleReadOnly" class="detail-lifecycle-banner">
              <strong v-if="mode === 'archived'">此任務已封存</strong>
              <strong v-else>此任務位於垃圾桶</strong>
            </div>

            <div v-else class="detail-toolbar-actions">
              <button
                v-if="!isEditing"
                type="button"
                class="primary-button"
                :disabled="isSaving || !canEnterEdit"
                @click="emitEnterEdit"
              >
                編輯
              </button>

              <button
                type="button"
                class="secondary-button"
                aria-haspopup="menu"
                :aria-expanded="isActionsOpen"
                :disabled="isSaving || isLifecycleReadOnly || isEditing || !canEnterEdit"
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
            <div class="detail-field">
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
                  :manual-input="true"
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

          <div class="detail-card detail-card-full" data-testid="task-reminders-section">
            <div class="detail-section-header">
              <div>
                <p class="detail-label">提醒</p>
                <p v-if="reminderDeliveryStatusMessage" class="detail-supporting-copy" data-testid="reminder-delivery-status">
                  {{ reminderDeliveryStatusMessage }}
                </p>
              </div>

              <button
                v-if="canEnableReminderDelivery"
                type="button"
                class="secondary-button"
                :disabled="isSaving || isEnablingReminderDelivery"
                @click="emitEnableReminderDelivery"
              >
                {{ isEnablingReminderDelivery ? '啟用中...' : '啟用提醒通知' }}
              </button>
            </div>

            <div class="detail-reminder-grid">
              <div class="detail-field">
                <label class="detail-label" for="task-reminder-datetime-input">提醒時間</label>

                <div class="detail-field-control">
                  <InputText
                    id="task-reminder-datetime-input"
                    v-model="draftReminderDateTime"
                    type="datetime-local"
                    fluid
                    :disabled="isSaving || isReadOnly"
                  />
                  <p v-if="reminderValidationError" class="error-copy">{{ reminderValidationError }}</p>
                </div>
              </div>

              <div class="detail-field detail-field-inline">
                <label class="detail-label" for="task-reminder-description-input">提醒說明</label>

                <div class="detail-field-control">
                  <Textarea
                    id="task-reminder-description-input"
                    v-model="draftReminderDescription"
                    fluid
                    auto-resize
                    rows="3"
                    :disabled="isSaving || isReadOnly"
                  />
                </div>
              </div>
            </div>

            <div class="detail-reminder-actions">
              <button type="button" class="secondary-button" :disabled="isSaving || isReadOnly" @click="submitReminder">新增提醒</button>
            </div>

            <div class="detail-reminder-list">
              <p v-if="taskReminders.length === 0" class="detail-supporting-copy">尚未設定提醒</p>

              <ul v-else class="history-list">
                <li v-for="reminder in taskReminders" :key="reminder.id" data-testid="task-reminder-item">
                  <div class="detail-reminder-item-copy">
                    <span>{{ formatReminderDateTime(reminder.reminderDateTime) }}</span>
                    <small v-if="reminder.description">{{ reminder.description }}</small>
                  </div>
                  <button
                    v-if="!isReadOnly"
                    type="button"
                    class="detail-actions-menu-item"
                    :disabled="isSaving"
                    @click="emitDeleteReminder(reminder)"
                  >
                    刪除提醒
                  </button>
                </li>
              </ul>
            </div>
          </div>

          <div class="detail-card detail-card-full">
            <ApiCommandResourceView
              :is-submitting="isSaving"
              :error-message="saveErrorMessage"
              submitting-message="正在提交任務操作，請稍候..."
            />

            <div class="modal-actions">
              <button v-if="!isReadOnly" type="button" class="primary-button" :disabled="isSaving" @click="submit">儲存變更</button>
              <button v-else-if="isLifecycleReadOnly" type="button" class="primary-button" :disabled="isSaving" @click="emitRestore">還原</button>
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
import type { TaskDetailResponse, TaskReminderResponse } from '../api/ronflowApi'
import type { TaskDetailMode } from '../composables/useRonFlowBoard'

const props = withDefaults(defineProps<{
  isOpen: boolean
  isLoading: boolean
  isSaving: boolean
  isEditing?: boolean
  canEnterEdit?: boolean
  errorMessage: string
  saveErrorMessage: string
  titleValidationError: string
  reminderDateTimeValidationError?: string
  reminderDatetimeValidationError?: string
  reminderDeliveryStatusMessage: string
  canEnableReminderDelivery: boolean
  isEnablingReminderDelivery: boolean
  mode: TaskDetailMode
  displayTitle: string
  task: TaskDetailResponse | null
  formatTimelineTime: (occurredAt: string) => string
}>(), {
  isEditing: false,
  canEnterEdit: true,
})

const emit = defineEmits<{
  (event: 'close'): void
  (event: 'enter-edit'): void
  (event: 'save', payload: { taskId: string; title: string; description: string; dueDate: string | null }): void
  (event: 'add-reminder', payload: { taskId: string; reminderDateTime: string; description: string }): void
  (event: 'delete-reminder', payload: { taskId: string; reminderId: string }): void
  (event: 'enable-reminder-delivery'): void
  (event: 'archive', taskId: string): void
  (event: 'move-to-trash', taskId: string): void
  (event: 'restore', taskId: string, mode: Exclude<TaskDetailMode, 'active'>): void
}>()

const draftTitle = ref('')
const draftDescription = ref('')
const draftDueDate = ref('')
const draftReminderDateTime = ref('')
const draftReminderDescription = ref('')
const isActionsOpen = ref(false)

const isLifecycleReadOnly = computed(() => props.mode !== 'active')
const canEnterEdit = computed(() => !isLifecycleReadOnly.value && (props.canEnterEdit ?? true))
const isEditing = computed(() => !isLifecycleReadOnly.value && Boolean(props.isEditing))
const isReadOnly = computed(() => isLifecycleReadOnly.value || !isEditing.value)
const taskReminders = computed(() => props.task?.reminders ?? [])
const reminderValidationError = computed(() =>
  props.reminderDatetimeValidationError
  ?? props.reminderDateTimeValidationError
  ?? '',
)

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

function normalizeDateTimeLocalInput(value: string): string {
  const trimmedValue = value.trim()

  if (!trimmedValue) {
    return ''
  }

  return trimmedValue.replace(' ', 'T')
}

function resolveReminderDateTime(): string {
  if (draftReminderDateTime.value.trim()) {
    return draftReminderDateTime.value
  }

  if (typeof document === 'undefined') {
    return ''
  }

  const reminderInput = document.getElementById('task-reminder-datetime-input') as HTMLInputElement | null
  const nextValue = normalizeDateTimeLocalInput(reminderInput?.value ?? '')

  draftReminderDateTime.value = nextValue

  return nextValue
}

function formatReminderDateTime(value: string): string {
  const localMatch = value.match(/^(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2})/)

  if (localMatch) {
    return `${localMatch[1]} ${localMatch[2]}`
  }

  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return value
  }

  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  const hour = String(date.getHours()).padStart(2, '0')
  const minute = String(date.getMinutes()).padStart(2, '0')

  return `${year}-${month}-${day} ${hour}:${minute}`
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

function submitReminder() {
  if (!props.task || props.isSaving || isReadOnly.value) {
    return
  }

  const reminderDateTime = resolveReminderDateTime()

  emit('add-reminder', {
    taskId: props.task.id,
    reminderDateTime,
    description: draftReminderDescription.value,
  })
}

function emitDeleteReminder(reminder: TaskReminderResponse) {
  if (!props.task || props.isSaving || isReadOnly.value) {
    return
  }

  emit('delete-reminder', {
    taskId: props.task.id,
    reminderId: reminder.id,
  })
}

function emitEnableReminderDelivery() {
  if (props.isSaving || props.isEnablingReminderDelivery) {
    return
  }

  emit('enable-reminder-delivery')
}

function toggleActionsMenu() {
  if (props.isSaving || isLifecycleReadOnly.value || isEditing.value || !canEnterEdit.value) {
    return
  }

  isActionsOpen.value = !isActionsOpen.value
}

function emitEnterEdit() {
  if (props.isSaving || !canEnterEdit.value || isEditing.value) {
    return
  }

  emit('enter-edit')
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
  () => [
    props.isOpen,
    props.isEditing,
    props.task?.id,
    props.task?.title,
    props.task?.description,
    props.task?.dueDate,
    props.task?.reminders?.length ?? 0,
    props.mode,
  ] as const,
  ([isOpen]) => {
    isActionsOpen.value = false

    if (!isOpen || !props.task) {
      return
    }

    draftTitle.value = props.task.title
    draftDescription.value = props.task.description
    draftDueDate.value = props.task.dueDate ?? ''
    draftReminderDateTime.value = ''
    draftReminderDescription.value = ''
  },
  { immediate: true },
)
</script>