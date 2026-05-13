<template>
  <section class="board-panel">
    <header class="board-header">
      <div>
        <p class="eyebrow">Task lifecycle</p>
        <h2 class="board-title">{{ title }}</h2>
      </div>

      <button type="button" class="secondary-button" @click="$emit('back-to-board')">
        返回看板
      </button>
    </header>

    <AsyncStateBoundary
      :is-loading="isLoading"
      :error-message="errorMessage"
      :loading-message="loadingMessage"
    >
      <div v-if="items.length === 0" class="board-empty-state">
        <p class="eyebrow">Outside-In slice</p>
        <p class="empty-copy">{{ emptyMessage }}</p>
        <p>{{ description }}</p>
      </div>

      <div v-else class="lifecycle-list">
        <article v-for="task in items" :key="task.id" class="lifecycle-task-card">
          <div class="lifecycle-task-copy">
            <button type="button" class="lifecycle-task-link" @click="$emit('open-task-detail', task.id)">
              {{ task.title }}
            </button>
            <p class="task-meta">{{ task.projectName }}</p>
          </div>

          <dl class="lifecycle-task-details">
            <div>
              <dt>原欄位狀態</dt>
              <dd>{{ task.originalState.label }}</dd>
            </div>
            <div>
              <dt>{{ timeLabel }}</dt>
              <dd>{{ formatTimelineTime(task.changedAt) }}</dd>
            </div>
          </dl>

          <div class="modal-actions lifecycle-task-actions">
            <button type="button" class="secondary-button" @click="$emit('open-task-detail', task.id)">
              查看詳細資訊
            </button>
            <button type="button" class="primary-button" @click="$emit('restore-task', task.id)">
              還原
            </button>
          </div>
        </article>
      </div>
    </AsyncStateBoundary>
  </section>
</template>

<script setup lang="ts">
import AsyncStateBoundary from './bases/AsyncStateBoundary.vue'
import type { LifecycleTaskListItemResponse } from '../api/ronflowApi'

defineProps<{
  title: string
  emptyMessage: string
  description: string
  items: LifecycleTaskListItemResponse[]
  isLoading: boolean
  errorMessage: string
  loadingMessage: string
  timeLabel: string
  formatTimelineTime: (occurredAt: string) => string
}>()

defineEmits<{
  (event: 'back-to-board'): void
  (event: 'open-task-detail', taskId: string): void
  (event: 'restore-task', taskId: string): void
}>()
</script>