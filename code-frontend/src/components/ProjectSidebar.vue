<template>
  <aside class="project-panel">
    <div class="panel-heading-row">
      <div>
        <p class="eyebrow">Workspace</p>
        <h2 class="panel-title">專案列表</h2>
      </div>
      <span class="count-badge">{{ projects.length }}</span>
    </div>

    <p v-if="isLoadingProjects" class="empty-copy">正在載入專案列表...</p>

    <p v-else-if="projects.length === 0" class="empty-copy">尚未建立任何專案</p>

    <ul v-else class="project-list">
      <li v-for="project in projects" :key="project.id">
        <button
          type="button"
          class="project-chip"
          :class="{ 'project-chip-active': project.id === activeProjectId }"
          @click="$emit('select-project', project.id)"
        >
          <span>{{ project.name }}</span>
          <small>{{ formatProjectMeta(project.updatedAt) }}</small>
        </button>
      </li>
    </ul>
  </aside>
</template>

<script setup lang="ts">
import type { ProjectListItemResponse } from '../api/ronflowApi'

defineProps<{
  projects: ProjectListItemResponse[]
  activeProjectId: string | null
  isLoadingProjects: boolean
  formatProjectMeta: (updatedAt: string) => string
}>()

defineEmits<{
  (event: 'select-project', projectId: string): void
}>()
</script>