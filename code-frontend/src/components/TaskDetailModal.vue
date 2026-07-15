<template>
  <BaseModalShell
    :is-open="isOpen"
    :close-disabled="isSaving"
    allow-underlay-interaction
    title="任務詳細資訊"
    title-id="task-detail-title"
    eyebrow="Task detail"
    size="wide"
    :presentation="viewMode"
    :close-on-escape="canAutoCloseDrawer"
    :close-on-interact-outside="canAutoCloseDrawer"
    @close="$emit('close')"
  >
    <template #header-actions>
      <div v-if="!isForcedReadOnly" class="detail-header-actions" data-testid="task-detail-header-actions">
        <button
          v-if="isEditing"
          type="button"
          class="primary-button"
          :disabled="isSaving"
          @click="submit"
        >
          儲存變更
        </button>

        <template v-else-if="!isLifecycleReadOnly">
          <button
            v-if="canToggleSplitCompleteAction"
            type="button"
            class="secondary-button"
            data-testid="task-split-complete-toggle"
            :disabled="isSaving || isEditing || !canEnterEdit"
            @click="emitToggleSplitComplete"
          >
            {{ splitCompleteActionLabel }}
          </button>

          <button
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
        </template>

        <button
          v-else
          type="button"
          class="primary-button"
          :disabled="isSaving"
          @click="emitRestore"
        >
          還原
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
    </template>

      <section v-if="!task && displayTitle" class="detail-preview-header">
        <div class="detail-preview-copy">
          <h3>{{ displayTitle }}</h3>
        </div>
      </section>

      <AsyncStateBoundary
        :is-loading="isLoading"
        :error-message="errorMessage"
        loading-message="正在載入任務詳細資訊..."
      >
        <section v-if="task" class="detail-preview-header">
          <div class="detail-preview-copy" data-testid="task-detail-title-header">
            <template v-if="isEditing">
              <label class="detail-label" for="task-detail-title-input">任務標題</label>
              <InputText
                id="task-detail-title-input"
                v-model="draftTitle"
                fluid
                :disabled="isSaving || isReadOnly"
                :invalid="Boolean(titleValidationError)"
              />
              <p v-if="titleValidationError" class="error-copy">{{ titleValidationError }}</p>
            </template>

            <template v-else>
              <h3>{{ task.title }}</h3>
            </template>

            <p class="detail-preview-meta">建立 {{ formatTimelineTime(task.createdAt) }}</p>
            <p v-if="isLifecycleReadOnly" class="detail-preview-status">
              <strong v-if="mode === 'archived'">此任務已封存</strong>
              <strong v-else>此任務位於垃圾桶</strong>
            </p>
          </div>
        </section>

        <ApiCommandResourceView
          v-if="task && (isSaving || saveErrorMessage)"
          class="detail-command-status"
          :is-submitting="isSaving"
          :error-message="saveErrorMessage"
          submitting-message="正在提交任務操作，請稍候..."
        />

        <section v-if="task" class="detail-layout">

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

          <div
            v-if="showsSplitCompleteStatus"
            class="detail-card"
            :class="{ 'detail-card-split-complete': task.isSplitComplete }"
            data-testid="task-split-complete-status"
          >
            <p class="detail-label">拆解狀態</p>
            <strong>{{ task.isSplitComplete ? '拆解完成' : '拆解中' }}</strong>
          </div>

          <div v-if="task.parentPath" class="detail-card">
            <p class="detail-label">任務樹位置</p>
            <strong>{{ task.parentPath }}</strong>
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

          <div v-if="task.currentState.isCompletedState && task.completedAt" class="detail-card">
            <p class="detail-label">完成時間</p>
            <strong>{{ formatTimelineTime(task.completedAt) }}</strong>
          </div>

          <div class="detail-card detail-card-full" data-testid="task-child-tasks-section">
            <div class="detail-section-header">
              <div>
                <p class="detail-label">Child tasks</p>
                <p v-if="isParentTask" class="detail-supporting-copy" data-testid="task-child-progress">
                  直接子任務進度 {{ completedChildTaskCount }} / {{ childTasks.length }}
                </p>
              </div>
            </div>

            <div v-if="canCreateChildTask" class="detail-reminder-grid">
              <div class="detail-field detail-field-inline">
                <label class="detail-label" for="task-child-title-input">Child task 標題</label>
                <div class="detail-field-control">
                  <InputText
                    id="task-child-title-input"
                    v-model="draftChildTaskTitle"
                    fluid
                    :disabled="isSaving"
                    @keydown.enter.prevent="submitChildTask"
                  />
                </div>
              </div>

              <div class="detail-reminder-actions">
                <button type="button" class="secondary-button" :disabled="isSaving" @click="submitChildTask">建立 child task</button>
              </div>
            </div>

            <p v-if="childTasks.length === 0" class="detail-supporting-copy">目前沒有 child task</p>

            <ul v-else class="history-list">
              <li v-for="childTask in childTasks" :key="childTask.id" data-testid="task-child-item">
                <button
                  type="button"
                  class="task-card-main"
                  :class="{ 'task-card-main-split-complete': childTask.isSplitComplete }"
                  @click="emitOpenChildTask(childTask.id, childTask.title)"
                >
                  <span class="task-title">
                    {{ childTask.title }}
                    <span
                      v-if="childTask.isSplitComplete"
                      class="task-status-badge task-status-badge-split-complete"
                    >
                      拆解完成
                    </span>
                  </span>
                  <span class="task-meta">Child task</span>
                </button>
              </li>
            </ul>

          </div>

          <div v-if="!isParentTask" class="detail-card detail-card-full detail-ready-list" data-testid="task-ready-list-section">
            <div class="detail-section-header">
              <div>
                <p class="detail-label">Ready list</p>
              </div>

              <button
                v-if="!isReadOnly"
                type="button"
                class="secondary-button"
                :disabled="isSaving"
                @click="addSubtask"
              >
                新增完成條件
              </button>

              <button
                v-if="canApplyCompletionTemplate"
                type="button"
                class="secondary-button"
                data-testid="task-apply-completion-template"
                :disabled="isSaving || isLoadingCompletionTemplates || completionTemplates.length === 0"
                title="會以目前專案模板取代全部完成條件，並清除勾選狀態"
                @click="applyCompletionTemplate"
              >
                套用完成條件模板
              </button>
            </div>

            <div class="detail-ready-grid">
              <div class="detail-field">
                <label class="detail-label" for="task-detail-estimated-effort-value">預估耗時</label>

                <div class="detail-reminder-grid">
                  <div class="detail-field-control">
                    <InputText
                      id="task-detail-estimated-effort-value"
                      v-model="draftEstimatedEffortValue"
                      fluid
                      inputmode="numeric"
                      type="number"
                      min="1"
                      :disabled="isSaving || isReadOnly"
                    />
                  </div>

                  <div class="detail-field-control">
                    <select
                      v-model="draftEstimatedEffortUnit"
                      :disabled="isSaving || isReadOnly"
                    >
                      <option value="minutes">分鐘</option>
                      <option value="hours">小時</option>
                      <option value="days">天</option>
                    </select>
                  </div>
                </div>
              </div>

              <div class="detail-ready-action">
                <button
                  v-if="canShowSendToFlow"
                  type="button"
                  class="primary-button"
                  :disabled="isSaving || isForcedReadOnly || isEditing || !isReadyForFlow"
                  @click="emitSendToFlow"
                >
                  送進 Flow
                </button>
              </div>
            </div>

            <p class="detail-label">完成條件</p>
            <p v-if="draftSubtasks.length === 0" class="detail-supporting-copy">目前沒有完成條件</p>

            <ul v-else class="history-list">
              <li v-for="(subtask, index) in draftSubtasks" :key="subtask.id ?? `draft-${index}`" data-testid="task-subtask-item">
                <div class="detail-checklist-row">
                  <input
                    :id="`task-subtask-${index}`"
                    :checked="subtask.isChecked"
                    type="checkbox"
                    :disabled="isSaving || !canToggleSubtaskCheckbox"
                    @change="onSubtaskCheckedChanged(index, $event)"
                  >

                  <InputText
                    :model-value="subtask.title"
                    fluid
                    :disabled="isSaving || isChecklistTextReadOnly"
                    @update:model-value="updateSubtaskTitle(index, $event)"
                  />

                  <div v-if="!isReadOnly" class="detail-checklist-actions">
                    <button type="button" class="secondary-button" :disabled="isSaving || index === 0" @click="moveSubtask(index, -1)">上移</button>
                    <button type="button" class="secondary-button" :disabled="isSaving || index === draftSubtasks.length - 1" @click="moveSubtask(index, 1)">下移</button>
                    <button type="button" class="detail-actions-menu-item" :disabled="isSaving" @click="removeSubtask(index)">刪除</button>
                  </div>
                </div>
              </li>
            </ul>

            <label v-if="isEditing" class="detail-checklist-row" for="task-detail-is-short">
              <input
                id="task-detail-is-short"
                v-model="draftIsShort"
                type="checkbox"
                :disabled="isSaving || isReadOnly || (!canUseShortTask && !draftIsShort)"
              />
              <span>short 任務（自動設定 15 分鐘並送進 Flow）</span>
            </label>
            <p v-if="isEditing && !canUseShortTask" class="detail-supporting-copy">專案沒有完成條件模板，無法啟用 short 任務。</p>
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
                v-if="canEnableReminderDelivery && !isForcedReadOnly && !isLifecycleReadOnly"
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

          <div class="detail-card detail-card-full" data-testid="task-code-traceability-section">
            <div class="detail-section-header">
              <div>
                <p class="detail-label">程式修改追蹤</p>
                <p class="detail-supporting-copy">用結構化紀錄標示這個 task 影響到的 API、前端頁面與前端元件。</p>
              </div>
            </div>

            <div
              v-for="category in traceabilityCategories"
              :key="category.key"
              class="detail-field"
              :data-testid="`task-code-traceability-${category.testId}`"
            >
              <div class="detail-section-header">
                <div>
                  <p class="detail-label">{{ category.label }}</p>
                </div>

                <button
                  v-if="!isReadOnly"
                  type="button"
                  class="secondary-button"
                  :disabled="isSaving"
                  @click="addCodeTraceabilityItem(category.key)"
                >
                  新增紀錄
                </button>
              </div>

              <p v-if="draftCodeTraceability[category.key].length === 0" class="detail-supporting-copy">目前沒有紀錄</p>

              <ul v-else class="history-list">
                <li
                  v-for="(item, index) in draftCodeTraceability[category.key]"
                  :key="`${category.key}-${index}`"
                  data-testid="task-code-traceability-item"
                >
                  <div v-if="isReadOnly" class="detail-checklist-row">
                    <strong>{{ formatTraceabilityChangeType(item.changeType) }}</strong>
                    <span>{{ item.target }}</span>
                  </div>

                  <div v-else class="detail-reminder-grid">
                    <div class="detail-field">
                      <label class="detail-label" :for="`task-code-traceability-${category.testId}-${index}-change-type`">變更類型</label>
                      <div class="detail-field-control">
                        <select
                          :id="`task-code-traceability-${category.testId}-${index}-change-type`"
                          :value="item.changeType"
                          :disabled="isSaving"
                          @change="onCodeTraceabilityChangeTypeChanged(category.key, index, $event)"
                        >
                          <option
                            v-for="option in traceabilityChangeTypeOptions"
                            :key="option.value"
                            :value="option.value"
                          >
                            {{ option.label }}
                          </option>
                        </select>
                      </div>
                    </div>

                    <div class="detail-field detail-field-inline">
                      <label class="detail-label" :for="`task-code-traceability-${category.testId}-${index}-target`">項目名稱</label>
                      <div class="detail-field-control">
                        <InputText
                          :id="`task-code-traceability-${category.testId}-${index}-target`"
                          :model-value="item.target"
                          fluid
                          :disabled="isSaving"
                          @update:model-value="updateCodeTraceabilityTarget(category.key, index, $event)"
                        />
                      </div>
                    </div>

                    <div class="detail-checklist-actions">
                      <button
                        type="button"
                        class="detail-actions-menu-item"
                        :disabled="isSaving"
                        @click="removeCodeTraceabilityItem(category.key, index)"
                      >
                        刪除
                      </button>
                    </div>
                  </div>
                </li>
              </ul>
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
import type {
  ProjectSubtaskTemplateResponse,
  TaskCodeTraceabilityChangeType,
  TaskDetailResponse,
  TaskEstimatedEffortUnit,
  TaskReminderResponse,
} from '../api/ronflowApi'
import { ProjectQueryService } from '../application'
import type { TaskDetailMode } from '../composables/useRonFlowBoard'

type DraftTaskSubtask = {
  id: string | null
  title: string
  isChecked: boolean
}

type DraftTaskCodeTraceabilityItem = {
  changeType: TaskCodeTraceabilityChangeType
  target: string
}

type DraftTaskCodeTraceability = {
  api: DraftTaskCodeTraceabilityItem[]
  frontendPages: DraftTaskCodeTraceabilityItem[]
  frontendComponents: DraftTaskCodeTraceabilityItem[]
}

type TraceabilityCategoryKey = keyof DraftTaskCodeTraceability

const props = withDefaults(defineProps<{
  isOpen: boolean
  isLoading: boolean
  isSaving: boolean
  isEditing?: boolean
  isReadOnly?: boolean
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
  viewMode?: 'drawer' | 'modal'
  displayTitle: string
  task: TaskDetailResponse | null
  formatTimelineTime: (occurredAt: string) => string
}>(), {
  isEditing: false,
  isReadOnly: false,
  canEnterEdit: true,
  viewMode: 'drawer',
})

const emit = defineEmits<{
  (event: 'close'): void
  (event: 'enter-edit'): void
  (event: 'save', payload: {
    taskId: string
    title: string
    description: string
    dueDate: string | null
    estimatedEffort: { value: number; unit: TaskEstimatedEffortUnit } | null
    isShort: boolean
    codeTraceability: DraftTaskCodeTraceability
    subtasks: Array<{ id: string | null; title: string; isChecked: boolean; order: number }>
  }): void
  (event: 'send-to-flow', taskId: string): void
  (event: 'replace-subtasks', payload: {
    taskId: string
    subtasks: Array<{ id: string | null; title: string; isChecked: boolean; order: number }>
  }): void
  (event: 'set-split-complete', payload: { taskId: string; isSplitComplete: boolean }): void
  (event: 'create-child-task', payload: { parentTaskId: string; title: string }): void
  (event: 'open-child-task', taskId: string, taskTitle: string): void
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
const draftEstimatedEffortValue = ref('')
const draftEstimatedEffortUnit = ref<TaskEstimatedEffortUnit>('hours')
const draftIsShort = ref(false)
const draftReminderDateTime = ref('')
const draftReminderDescription = ref('')
const draftChildTaskTitle = ref('')
const draftSubtasks = ref<DraftTaskSubtask[]>([])
const draftCodeTraceability = ref<DraftTaskCodeTraceability>(createEmptyCodeTraceability())
const isActionsOpen = ref(false)
const canUseShortTask = ref(false)
const completionTemplates = ref<ProjectSubtaskTemplateResponse[]>([])
const isLoadingCompletionTemplates = ref(false)
const projectQueryService = new ProjectQueryService()

const traceabilityCategories: Array<{ key: TraceabilityCategoryKey; label: string; testId: string }> = [
  { key: 'api', label: 'API', testId: 'api' },
  { key: 'frontendPages', label: '前端頁面', testId: 'frontend-pages' },
  { key: 'frontendComponents', label: '前端元件', testId: 'frontend-components' },
]

const traceabilityChangeTypeOptions: Array<{ value: TaskCodeTraceabilityChangeType; label: string }> = [
  { value: 'added', label: '新增' },
  { value: 'modified', label: '修改' },
  { value: 'removed', label: '移除' },
]

const isForcedReadOnly = computed(() => Boolean(props.isReadOnly))
const isLifecycleReadOnly = computed(() => props.mode !== 'active')
const canEnterEdit = computed(() => !isForcedReadOnly.value && !isLifecycleReadOnly.value && (props.canEnterEdit ?? true))
const isEditing = computed(() => !isForcedReadOnly.value && !isLifecycleReadOnly.value && Boolean(props.isEditing))
const isReadOnly = computed(() => isForcedReadOnly.value || isLifecycleReadOnly.value || !isEditing.value)
const canAutoCloseDrawer = computed(() => props.viewMode === 'drawer' && !isEditing.value && !props.isSaving)
const canToggleSubtaskCheckbox = computed(() => !isForcedReadOnly.value && !isLifecycleReadOnly.value && canEnterEdit.value)
const isChecklistTextReadOnly = computed(() => isForcedReadOnly.value || isLifecycleReadOnly.value || !isEditing.value)
const taskReminders = computed(() => props.task?.reminders ?? [])
const childTasks = computed(() => props.task?.childTasks ?? [])
const isParentTask = computed(() => childTasks.value.length > 0)
const canApplyCompletionTemplate = computed(() =>
  Boolean(props.task)
  && !isForcedReadOnly.value
  && !isLifecycleReadOnly.value
  && !isEditing.value
  && canEnterEdit.value
  && !isParentTask.value,
)
const showsSplitCompleteStatus = computed(() => isParentTask.value || Boolean(props.task?.isSplitComplete))
const canToggleSplitCompleteAction = computed(() =>
  Boolean(props.task)
  && !isForcedReadOnly.value
  && !isLifecycleReadOnly.value
  && !props.task?.isInFlow
  && showsSplitCompleteStatus.value,
)
const splitCompleteActionLabel = computed(() => props.task?.isSplitComplete ? '取消拆解完成' : '標記拆解完成')
const completedChildTaskCount = computed(() => childTasks.value.filter((childTask) => childTask.isCompleted).length)
const canCreateChildTask = computed(() => !isForcedReadOnly.value && !isLifecycleReadOnly.value && canEnterEdit.value)
const hasCompletionCriteria = computed(() => draftSubtasks.value.some((subtask) => subtask.title.trim().length > 0))
const hasEstimatedEffort = computed(() => {
  const value = Number(draftEstimatedEffortValue.value)
  return Number.isInteger(value) && value > 0
})
const isReadyForFlow = computed(() => !isParentTask.value && hasCompletionCriteria.value && hasEstimatedEffort.value)
const canShowSendToFlow = computed(() => Boolean(props.task) && !props.task?.isInFlow && !isParentTask.value && !isLifecycleReadOnly.value)
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
    estimatedEffort: buildEstimatedEffortPayload(),
    isShort: draftIsShort.value,
    codeTraceability: buildCodeTraceabilityPayload(),
    subtasks: buildSubtaskPayload(),
  })
}

function addSubtask() {
  draftSubtasks.value = [
    ...draftSubtasks.value,
    {
      id: null,
      title: '',
      isChecked: false,
    },
  ]
}

function applyCompletionTemplate() {
  if (!props.task || props.isSaving || !canApplyCompletionTemplate.value || completionTemplates.value.length === 0) {
    return
  }

  emit('replace-subtasks', {
    taskId: props.task.id,
    subtasks: [...completionTemplates.value]
      .sort((left, right) => left.order - right.order)
      .map((template, index) => ({
        id: null,
        title: template.title,
        isChecked: false,
        order: index,
      })),
  })
}

function submitChildTask() {
  if (!props.task || props.isSaving || !canCreateChildTask.value) {
    return
  }

  emit('create-child-task', {
    parentTaskId: props.task.id,
    title: draftChildTaskTitle.value,
  })
}

function createEmptyCodeTraceability(): DraftTaskCodeTraceability {
  return {
    api: [],
    frontendPages: [],
    frontendComponents: [],
  }
}

function createDraftCodeTraceability(task: TaskDetailResponse | null): DraftTaskCodeTraceability {
  return {
    api: task?.codeTraceability.api.map((item) => ({ ...item })) ?? [],
    frontendPages: task?.codeTraceability.frontendPages.map((item) => ({ ...item })) ?? [],
    frontendComponents: task?.codeTraceability.frontendComponents.map((item) => ({ ...item })) ?? [],
  }
}

function buildCodeTraceabilityPayload(): DraftTaskCodeTraceability {
  return {
    api: draftCodeTraceability.value.api
      .map((item) => ({ changeType: item.changeType, target: item.target.trim() }))
      .filter((item) => item.target.length > 0),
    frontendPages: draftCodeTraceability.value.frontendPages
      .map((item) => ({ changeType: item.changeType, target: item.target.trim() }))
      .filter((item) => item.target.length > 0),
    frontendComponents: draftCodeTraceability.value.frontendComponents
      .map((item) => ({ changeType: item.changeType, target: item.target.trim() }))
      .filter((item) => item.target.length > 0),
  }
}

function buildSubtaskPayload(): Array<{ id: string | null; title: string; isChecked: boolean; order: number }> {
  return draftSubtasks.value
    .map((subtask) => ({
      id: subtask.id,
      title: subtask.title.trim(),
      isChecked: subtask.isChecked,
    }))
    .filter((subtask) => subtask.id !== null || subtask.title.length > 0)
    .map((subtask, index) => ({
      ...subtask,
      order: index,
    }))
}

function buildEstimatedEffortPayload(): { value: number; unit: TaskEstimatedEffortUnit } | null {
  const value = Number(draftEstimatedEffortValue.value)
  if (!Number.isInteger(value) || value <= 0) {
    return null
  }

  return {
    value,
    unit: draftEstimatedEffortUnit.value,
  }
}

function addCodeTraceabilityItem(categoryKey: TraceabilityCategoryKey) {
  draftCodeTraceability.value = {
    ...draftCodeTraceability.value,
    [categoryKey]: [
      ...draftCodeTraceability.value[categoryKey],
      { changeType: 'modified', target: '' },
    ],
  }
}

function onCodeTraceabilityChangeTypeChanged(categoryKey: TraceabilityCategoryKey, index: number, event: Event) {
  const nextValue = (event.target as HTMLSelectElement | null)?.value as TaskCodeTraceabilityChangeType | undefined
  updateCodeTraceabilityItem(categoryKey, index, {
    changeType: nextValue ?? 'modified',
  })
}

function updateCodeTraceabilityTarget(categoryKey: TraceabilityCategoryKey, index: number, value: string | undefined) {
  updateCodeTraceabilityItem(categoryKey, index, {
    target: value ?? '',
  })
}

function updateCodeTraceabilityItem(
  categoryKey: TraceabilityCategoryKey,
  index: number,
  patch: Partial<DraftTaskCodeTraceabilityItem>,
) {
  draftCodeTraceability.value = {
    ...draftCodeTraceability.value,
    [categoryKey]: draftCodeTraceability.value[categoryKey].map((item, currentIndex) =>
      currentIndex === index
        ? { ...item, ...patch }
        : item,
    ),
  }
}

function removeCodeTraceabilityItem(categoryKey: TraceabilityCategoryKey, index: number) {
  draftCodeTraceability.value = {
    ...draftCodeTraceability.value,
    [categoryKey]: draftCodeTraceability.value[categoryKey].filter((_, currentIndex) => currentIndex !== index),
  }
}

function formatTraceabilityChangeType(changeType: TaskCodeTraceabilityChangeType): string {
  switch (changeType) {
    case 'added':
      return '新增'
    case 'removed':
      return '移除'
    default:
      return '修改'
  }
}

function updateSubtaskTitle(index: number, value: string | undefined) {
  draftSubtasks.value = draftSubtasks.value.map((subtask, currentIndex) =>
    currentIndex === index
      ? { ...subtask, title: value ?? '' }
      : subtask,
  )
}

function onSubtaskCheckedChanged(index: number, event: Event) {
  const checkbox = event.target as HTMLInputElement | null
  const isChecked = checkbox?.checked ?? false

  draftSubtasks.value = draftSubtasks.value.map((subtask, currentIndex) =>
    currentIndex === index
      ? { ...subtask, isChecked }
      : subtask,
  )

  if (!props.task || props.isSaving || isLifecycleReadOnly.value || isEditing.value) {
    return
  }

  emit('replace-subtasks', {
    taskId: props.task.id,
    subtasks: buildSubtaskPayload(),
  })
}

function moveSubtask(index: number, direction: -1 | 1) {
  const targetIndex = index + direction
  if (targetIndex < 0 || targetIndex >= draftSubtasks.value.length) {
    return
  }

  const nextSubtasks = [...draftSubtasks.value]
  const [current] = nextSubtasks.splice(index, 1)
  nextSubtasks.splice(targetIndex, 0, current)
  draftSubtasks.value = nextSubtasks
}

function removeSubtask(index: number) {
  draftSubtasks.value = draftSubtasks.value.filter((_, currentIndex) => currentIndex !== index)
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

function emitOpenChildTask(taskId: string, taskTitle: string) {
  if (props.isSaving) {
    return
  }

  emit('open-child-task', taskId, taskTitle)
}

function emitEnableReminderDelivery() {
  if (props.isSaving || props.isEnablingReminderDelivery) {
    return
  }

  emit('enable-reminder-delivery')
}

function toggleActionsMenu() {
  if (props.isSaving || isForcedReadOnly.value || isLifecycleReadOnly.value || isEditing.value || !canEnterEdit.value) {
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
  if (!props.task || props.isSaving || isForcedReadOnly.value) {
    return
  }

  isActionsOpen.value = false
  emit('archive', props.task.id)
}

function emitMoveToTrash() {
  if (!props.task || props.isSaving || isForcedReadOnly.value) {
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

function emitSendToFlow() {
  if (!props.task || props.isSaving || isForcedReadOnly.value || isEditing.value || !isReadyForFlow.value) {
    return
  }

  emit('send-to-flow', props.task.id)
}

function emitToggleSplitComplete() {
  if (!props.task || props.isSaving || isEditing.value || !canToggleSplitCompleteAction.value || !canEnterEdit.value) {
    return
  }

  emit('set-split-complete', {
    taskId: props.task.id,
    isSplitComplete: !props.task.isSplitComplete,
  })
}

watch(
  [
    () => props.isOpen,
    () => props.isEditing,
    () => props.task?.id ?? null,
    () => props.task?.title ?? '',
    () => props.task?.description ?? '',
    () => props.task?.dueDate ?? null,
    () => props.task?.estimatedEffort?.value ?? null,
    () => props.task?.estimatedEffort?.unit ?? null,
    () => props.task?.subtasks?.length ?? 0,
    () => props.task?.childTasks?.length ?? 0,
    () => props.task?.codeTraceability?.api?.length ?? 0,
    () => props.task?.codeTraceability?.frontendPages?.length ?? 0,
    () => props.task?.codeTraceability?.frontendComponents?.length ?? 0,
    () => props.task?.reminders?.length ?? 0,
    () => props.mode,
  ] as const,
  (
    [
      isOpen,
      ,
      taskId,
      ,
      ,
      ,
      ,
      ,
      ,
      childTaskCount,
    ],
    [
      wasOpen,
      ,
      previousTaskId,
      ,
      ,
      ,
      ,
      ,
      ,
      previousChildTaskCount,
    ] = [false, false, null, '', '', null, null, null, 0, 0, 0, 0, 0, 0, 'active'],
  ) => {
    isActionsOpen.value = false

    if (!isOpen || !props.task) {
      return
    }

    draftTitle.value = props.task.title
    draftDescription.value = props.task.description
    draftDueDate.value = props.task.dueDate ?? ''
    draftEstimatedEffortValue.value = props.task.estimatedEffort?.value ? String(props.task.estimatedEffort.value) : ''
    draftEstimatedEffortUnit.value = props.task.estimatedEffort?.unit ?? 'hours'
    draftIsShort.value = props.task.isShort
    draftReminderDateTime.value = ''
    draftReminderDescription.value = ''
    if (!wasOpen || taskId !== previousTaskId || childTaskCount !== previousChildTaskCount) {
      draftChildTaskTitle.value = ''
    }
    draftCodeTraceability.value = createDraftCodeTraceability(props.task)
    draftSubtasks.value = props.task.subtasks.map((subtask) => ({
      id: subtask.id,
      title: subtask.title,
      isChecked: subtask.isChecked,
    }))
  },
  { immediate: true },
)

watch(
  () => [props.isOpen, props.task?.projectId] as const,
  async ([isOpen, projectId]) => {
    canUseShortTask.value = false
    completionTemplates.value = []
    isLoadingCompletionTemplates.value = false
    if (!isOpen || !projectId) {
      return
    }

    isLoadingCompletionTemplates.value = true
    try {
      const templates = await projectQueryService.getSubtaskTemplates(projectId)
      completionTemplates.value = templates.items
      canUseShortTask.value = templates.items.length > 0
    } catch {
      canUseShortTask.value = false
    } finally {
      isLoadingCompletionTemplates.value = false
    }
  },
  { immediate: true },
)
</script>
