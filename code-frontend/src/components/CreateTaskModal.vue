<template>
  <div v-if="isOpen" class="modal-backdrop">
    <div role="dialog" aria-modal="true" aria-labelledby="create-task-title" class="modal-card">
      <div class="modal-header">
        <div>
          <p class="eyebrow">New task</p>
          <h2 id="create-task-title">建立任務</h2>
        </div>
        <button
          type="button"
          class="ghost-icon-button"
          aria-label="關閉視窗"
          :disabled="isSubmitting"
          @click="close"
        >
          ×
        </button>
      </div>

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
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { ApiRequestError, ApiValidationError } from '../api/ronflowApi'
import { TaskCommandService } from '../application'
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
const taskTitleInputRef = ref<HTMLInputElement | null>(null)

const taskCommandService = new TaskCommandService()
const createTaskResource = useApiResource(
  (projectId: string, title: string) => taskCommandService.create(projectId, title),
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
  createTaskResource.reset()
  isOpen.value = true
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
    await createTaskResource.execute(activeProjectId.value, taskTitle.value)
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