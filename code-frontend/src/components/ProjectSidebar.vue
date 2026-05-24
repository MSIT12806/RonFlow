<template>
  <aside class="project-panel">
    <div class="panel-heading-row">
      <div>
        <p class="eyebrow">Workspace</p>
        <h2 class="panel-title">專案列表</h2>
      </div>
      <span class="count-badge">{{ projects.length }}</span>
    </div>

    <div class="project-panel-actions">
      <button type="button" class="secondary-button" @click="$emit('open-invitation-inbox')">
        邀請收件匣
      </button>
      <span
        v-if="invitationInboxCount > 0"
        class="count-badge"
        data-testid="invitation-inbox-badge"
      >
        {{ invitationInboxCount }}
      </span>
    </div>

    <AsyncStateBoundary
      :is-loading="isLoadingProjects"
      error-message=""
      loading-message="正在載入專案列表..."
    >
      <p v-if="!hasError && projects.length === 0" class="empty-copy">尚未建立任何專案</p>

      <ul v-else class="project-list">
        <li v-for="project in projects" :key="project.id">
          <button
            type="button"
            class="project-chip"
            :class="{ 'project-chip-active': project.id === activeProjectId }"
            @click="$emit('select-project', project.id)"
          >
            <span class="project-chip-title">{{ project.name }}</span>
            <small>{{ formatProjectMeta(project.updatedAt) }}</small>
            <small class="project-chip-role">{{ project.role || '專案擁有者' }}</small>
          </button>
        </li>
      </ul>
    </AsyncStateBoundary>
  </aside>
</template>

<script setup lang="ts">
import AsyncStateBoundary from './bases/AsyncStateBoundary.vue'
import type { ProjectListItemResponse } from '../api/ronflowApi'

defineProps<{
  projects: ProjectListItemResponse[]
  activeProjectId: string | null
  invitationInboxCount: number
  isLoadingProjects: boolean
  hasError: boolean
  formatProjectMeta: (updatedAt: string) => string
}>()

defineEmits<{
  (event: 'select-project', projectId: string): void
  (event: 'open-invitation-inbox'): void
}>()
</script>