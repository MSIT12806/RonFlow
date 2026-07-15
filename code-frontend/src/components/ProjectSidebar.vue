<template>
  <aside class="project-panel">
    <div class="panel-heading-row">
      <div>
        <p class="eyebrow">Workspace</p>
        <h2 class="panel-title">專案列表</h2>
      </div>
      <span class="count-badge">{{ props.projects.length }}</span>
    </div>

    <div class="project-panel-actions">
      <button type="button" class="secondary-button" @click="$emit('open-invitation-inbox')">
        邀請收件匣
      </button>
      <span
        v-if="props.invitationInboxCount > 0"
        class="count-badge"
        data-testid="invitation-inbox-badge"
      >
        {{ props.invitationInboxCount }}
      </span>
    </div>

    <AsyncStateBoundary
      :is-loading="props.isLoadingProjects"
      error-message=""
      loading-message="正在載入專案列表..."
    >
      <p v-if="!props.hasError && props.projects.length === 0" class="empty-copy">尚未建立任何專案</p>

      <ul v-else class="project-list">
        <li v-for="project in props.projects" :key="project.id">
          <button
            type="button"
            class="project-chip"
            :class="{ 'project-chip-active': project.id === props.activeProjectId }"
            @click="$emit('select-project', project.id)"
          >
            <span class="project-chip-title">{{ project.name }}</span>
            <small>{{ props.formatProjectMeta(project.updatedAt) }}</small>
            <small class="project-chip-role">{{ project.role || '專案擁有者' }}</small>
            <small v-if="project.activeTaskCount > 0" class="project-chip-active-tasks">
              進行中任務：{{ project.activeTaskCount }}
            </small>
          </button>
        </li>
      </ul>
    </AsyncStateBoundary>

    <section v-if="props.projects.length > 0" class="project-sidebar-control">
      <div class="project-sidebar-control-header">
        <div>
          <p class="eyebrow">已完成篩選</p>
          <h3>{{ selectedVisibilityOption.label }}</h3>
        </div>
        <span class="count-badge project-sidebar-control-badge">{{ selectedIndex + 1 }}/{{ visibilityOptions.length }}</span>
      </div>

      <p class="project-sidebar-control-copy">
        只影響 Flow 看板中的「已完成」欄位，預設只顯示本月完成的 task。
      </p>

      <input
        class="project-sidebar-slider"
        type="range"
        min="0"
          :max="visibilityOptions.length - 1"
          step="1"
          :value="selectedIndex"
          aria-label="已完成任務顯示範圍"
        @input="onVisibilityInput"
      />

      <div class="project-sidebar-slider-scale" aria-hidden="true">
        <span
          v-for="option in visibilityOptions"
          :key="option.value"
          :class="{ 'project-sidebar-slider-scale-active': option.value === props.completedTasksVisibility }"
        >
          {{ option.shortLabel }}
        </span>
      </div>
    </section>
  </aside>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import AsyncStateBoundary from './bases/AsyncStateBoundary.vue'
import type { ProjectListItemResponse } from '../api/ronflowApi'
import {
  completedTasksVisibilityOptions,
  getCompletedTasksVisibilityIndex,
  getCompletedTasksVisibilityValue,
  type CompletedTasksVisibilityValue,
} from '../features/completedTasksVisibility'

const props = defineProps<{
  projects: ProjectListItemResponse[]
  activeProjectId: string | null
  invitationInboxCount: number
  isLoadingProjects: boolean
  hasError: boolean
  formatProjectMeta: (updatedAt: string) => string
  completedTasksVisibility: CompletedTasksVisibilityValue
}>()

const emit = defineEmits<{
  (event: 'select-project', projectId: string): void
  (event: 'open-invitation-inbox'): void
  (event: 'change-completed-tasks-visibility', value: CompletedTasksVisibilityValue): void
}>()

const visibilityOptions = completedTasksVisibilityOptions
const selectedIndex = computed(() => getCompletedTasksVisibilityIndex(props.completedTasksVisibility))
const selectedVisibilityOption = computed(() => visibilityOptions[selectedIndex.value] ?? visibilityOptions[0])

function onVisibilityInput(event: Event) {
  const nextIndex = Number.parseInt((event.target as HTMLInputElement).value, 10)
  emit('change-completed-tasks-visibility', getCompletedTasksVisibilityValue(Number.isNaN(nextIndex) ? 0 : nextIndex))
}
</script>
