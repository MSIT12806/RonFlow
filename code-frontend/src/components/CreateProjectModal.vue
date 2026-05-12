<template>
  <BaseModalShell
    :is-open="isOpen"
    title="建立專案"
    title-id="create-project-title"
    eyebrow="New project"
    :close-disabled="isSubmitting"
    @close="close"
  >
      <form class="modal-form" @submit.prevent="submit">
        <label for="project-name">專案名稱</label>
        <input
          ref="projectNameInputRef"
          id="project-name"
          v-model="projectName"
          type="text"
          autocomplete="off"
          :disabled="isSubmitting"
        />
        <p v-if="projectNameError" class="error-copy">{{ projectNameError }}</p>

        <ApiCommandResourceView
          :is-submitting="isSubmitting"
          :error-message="commandErrorMessage"
          submitting-message="正在建立專案，請稍候..."
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
import { ApiValidationError } from '../api/ronflowApi'
import { ProjectCommandService } from '../application'
import BaseModalShell from '../components/bases/BaseModalShell.vue'
import ApiCommandResourceView from '../components/bases/ApiCommandResourceView.vue'
import { useApiResource } from '../composables/useApiResource'

const emit = defineEmits<{
  (event: 'open'): void
  (event: 'close'): void
  (event: 'project-created', projectId: string): void
}>()

const isOpen = ref(false)
const projectName = ref('')
const projectNameError = ref('')
const projectNameInputRef = ref<HTMLInputElement | null>(null)

const projectCommandService = new ProjectCommandService()
const createProjectResource = useApiResource((name: string) => projectCommandService.create(name), {
  mapErrorMessage: (error) => error instanceof ApiValidationError ? '' : '建立專案失敗，請稍後再試。',
})
const isSubmitting = computed(() => createProjectResource.isLoading.value)
const commandErrorMessage = computed(() => createProjectResource.errorMessage.value)

function open() {
  projectName.value = ''
  projectNameError.value = ''
  createProjectResource.reset()
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

async function submit() {
  projectNameError.value = ''

  try {
    const project = await createProjectResource.execute(projectName.value)
    close()
    emit('project-created', project.id)
  } catch (error) {
    if (error instanceof ApiValidationError) {
      projectNameError.value = error.errors.name?.[0] ?? '專案名稱為必填欄位'
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
    projectNameInputRef.value?.focus()
  },
)

defineExpose({
  open,
  close,
})
</script>