<template>
  <BaseModalShell
    :is-open="isOpen"
    title="建立任務"
    title-id="create-task-title"
    eyebrow="New task"
    :close-disabled="isSubmitting"
    @close="close"
  >
      <form class="modal-form" @submit.prevent="submit">
        <label for="task-title">任務標題</label>
        <input
          ref="taskTitleInputRef"
          id="task-title"
          v-model="taskTitle"
          type="text"
          autocomplete="off"
          :disabled="isSubmitting"
        />
        <p v-if="taskTitleError" class="error-copy">{{ taskTitleError }}</p>

        <label class="detail-checklist-row" for="task-is-short">
          <input id="task-is-short" v-model="isShort" type="checkbox" :disabled="isSubmitting || !canUseShortTask" />
          <span>short 任務（自動設定 15 分鐘並送進 Flow）</span>
        </label>
        <p v-if="!canUseShortTask" class="detail-supporting-copy">專案沒有完成條件模板，無法使用 short 任務。</p>

        <ApiCommandResourceView
          :is-submitting="isSubmitting"
          :error-message="commandErrorMessage"
          submitting-message="正在建立任務，請稍候..."
        />

        <div class="modal-actions">
          <button type="button" class="secondary-button" :disabled="isSubmitting" @click="close">取消</button>
          <button type="submit" class="primary-button" :disabled="isSubmitting">建立</button>
        </div>
      </form>
  </BaseModalShell>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { ApiRequestError, ApiValidationError } from '../api/ronflowApi'
import { ProjectQueryService, TaskCommandService } from '../application'
import BaseModalShell from '../components/bases/BaseModalShell.vue'
import ApiCommandResourceView from '../components/bases/ApiCommandResourceView.vue'
import { useApiResource } from '../composables/useApiResource'

const emit = defineEmits<{
  (event: 'open'): void
  (event: 'close'): void
  (event: 'task-created'): void
}>()

const isOpen = ref(false)
const taskTitle = ref('')
const taskTitleError = ref('')
const isShort = ref(false)
const canUseShortTask = ref(false)
const taskTitleInputRef = ref<HTMLInputElement | null>(null)

const taskCommandService = new TaskCommandService()
const projectQueryService = new ProjectQueryService()
const createTaskResource = useApiResource(
  (projectId: string, title: string, shortTask: boolean) => taskCommandService.create(projectId, title, shortTask),
  {
    mapErrorMessage: (error) => {
      if (error instanceof ApiValidationError) {
        return ''
      }

      if (error instanceof ApiRequestError && error.status === 404) {
        return ''
      }

      return '建立任務失敗，請稍後再試。'
    },
  },
)
const isSubmitting = computed(() => createTaskResource.isLoading.value)
const commandErrorMessage = computed(() => createTaskResource.errorMessage.value)

function open(projectId: string) {
  activeProjectId.value = projectId
  taskTitle.value = ''
  taskTitleError.value = ''
  isShort.value = false
  canUseShortTask.value = false
  createTaskResource.reset()
  isOpen.value = true
  void loadShortTaskAvailability(projectId)
  emit('open')
}

function close() {
  if (isSubmitting.value) {
    return
  }

  isOpen.value = false
  emit('close')
}

const activeProjectId = ref<string>('')

async function submit() {
  taskTitleError.value = ''

  try {
    await createTaskResource.execute(activeProjectId.value, taskTitle.value, isShort.value)
    close()
    emit('task-created')
  } catch (error) {
    if (error instanceof ApiValidationError) {
      taskTitleError.value = error.errors.title?.[0] ?? '任務標題為必填欄位'
      return
    }

    if (error instanceof ApiRequestError && error.status === 404) {
      taskTitleError.value = '目前專案不存在，請重新整理專案列表。'
    }
  }
}

async function loadShortTaskAvailability(projectId: string) {
  try {
    const templates = await projectQueryService.getSubtaskTemplates(projectId)
    canUseShortTask.value = templates.items.length > 0
  } catch {
    canUseShortTask.value = false
  }
}

watch(
  () => isOpen.value,
  async (isOpenValue) => {
    if (!isOpenValue) {
      return
    }

    await nextTick()
    taskTitleInputRef.value?.focus()
  },
)

defineExpose({
  open,
  close,
})
</script>
