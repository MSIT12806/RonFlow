<template>
  <BaseModalShell
    :is-open="isOpen"
    :close-disabled="isSaving"
    title="完成條件模板"
    title-id="project-subtask-templates-title"
    eyebrow="Project checklist templates"
    size="wide"
    @close="$emit('close')"
  >
    <AsyncStateBoundary
      :is-loading="isLoading"
      :error-message="errorMessage"
      loading-message="正在載入完成條件模板..."
    >
      <section class="detail-layout">
        <div class="detail-card detail-card-full" data-testid="project-subtask-templates-section">
          <div class="detail-section-header">
            <div>
              <p class="detail-label">{{ projectName || '目前專案' }}</p>
              <p class="detail-supporting-copy">新建立的任務會自動繼承這些完成條件，之後仍可在 task detail 自行調整。</p>
            </div>

            <button type="button" class="secondary-button" :disabled="isSaving" @click="addTemplate">
              新增模板
            </button>
          </div>

          <p v-if="draftItems.length === 0" class="detail-supporting-copy">目前沒有預設模板</p>

          <ul v-else class="history-list">
            <li v-for="(item, index) in draftItems" :key="item.id ?? `draft-${index}`" data-testid="project-subtask-template-item">
              <div class="detail-checklist-row">
                <InputText
                  :model-value="item.title"
                  fluid
                  :disabled="isSaving"
                  @update:model-value="updateTitle(index, $event)"
                />

                <div class="detail-checklist-actions">
                  <button type="button" class="secondary-button" :disabled="isSaving || index === 0" @click="moveTemplate(index, -1)">上移</button>
                  <button type="button" class="secondary-button" :disabled="isSaving || index === draftItems.length - 1" @click="moveTemplate(index, 1)">下移</button>
                  <button type="button" class="detail-actions-menu-item" :disabled="isSaving" @click="removeTemplate(index)">刪除</button>
                </div>
              </div>
            </li>
          </ul>
        </div>

        <div class="detail-card detail-card-full">
          <ApiCommandResourceView
            :is-submitting="isSaving"
            :error-message="saveErrorMessage"
            submitting-message="正在儲存完成條件模板，請稍候..."
          />

          <div class="modal-actions">
            <button type="button" class="primary-button" :disabled="isSaving" @click="submit">儲存模板</button>
          </div>
        </div>
      </section>
    </AsyncStateBoundary>
  </BaseModalShell>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import InputText from 'primevue/inputtext'
import AsyncStateBoundary from './bases/AsyncStateBoundary.vue'
import ApiCommandResourceView from './bases/ApiCommandResourceView.vue'
import BaseModalShell from './bases/BaseModalShell.vue'
import type { ProjectSubtaskTemplateResponse } from '../api/ronflowApi'

type DraftProjectSubtaskTemplate = {
  id: string | null
  title: string
}

const props = defineProps<{
  isOpen: boolean
  isLoading: boolean
  isSaving: boolean
  errorMessage: string
  saveErrorMessage: string
  projectName: string | null
  items: ProjectSubtaskTemplateResponse[]
}>()

const emit = defineEmits<{
  (event: 'close'): void
  (event: 'save', payload: Array<{ id: string | null; title: string; order: number }>): void
}>()

const draftItems = ref<DraftProjectSubtaskTemplate[]>([])

function addTemplate() {
  draftItems.value = [...draftItems.value, { id: null, title: '' }]
}

function updateTitle(index: number, value: string | undefined) {
  draftItems.value = draftItems.value.map((item, currentIndex) =>
    currentIndex === index
      ? { ...item, title: value ?? '' }
      : item,
  )
}

function moveTemplate(index: number, direction: -1 | 1) {
  const targetIndex = index + direction
  if (targetIndex < 0 || targetIndex >= draftItems.value.length) {
    return
  }

  const nextItems = [...draftItems.value]
  const [current] = nextItems.splice(index, 1)
  nextItems.splice(targetIndex, 0, current)
  draftItems.value = nextItems
}

function removeTemplate(index: number) {
  draftItems.value = draftItems.value.filter((_, currentIndex) => currentIndex !== index)
}

function submit() {
  emit('save', draftItems.value.map((item, index) => ({
    id: item.id,
    title: item.title,
    order: index,
  })))
}

watch(
  () => [props.isOpen, props.items] as const,
  () => {
    if (!props.isOpen) {
      return
    }

    draftItems.value = props.items.map((item) => ({
      id: item.id,
      title: item.title,
    }))
  },
  { immediate: true },
)
</script>