<template>
  <div v-if="isOpen && task" class="modal-backdrop">
    <div role="dialog" aria-modal="true" aria-labelledby="task-detail-title" class="modal-card modal-card-wide">
      <div class="modal-header">
        <div>
          <p class="eyebrow">Task detail</p>
          <h2 id="task-detail-title">任務詳細資訊</h2>
        </div>
        <button type="button" class="ghost-icon-button" aria-label="關閉視窗" @click="$emit('close')">
          ×
        </button>
      </div>

      <section class="detail-layout">
        <div class="detail-card">
          <p class="detail-label">任務標題</p>
          <h3>{{ task.title }}</h3>
        </div>

        <div class="detail-card">
          <p class="detail-label">狀態</p>
          <strong>{{ task.currentState.label }}</strong>
        </div>

        <div class="detail-card">
          <p class="detail-label">建立時間</p>
          <strong>{{ formatTimelineTime(task.createdAt) }}</strong>
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
    </div>
  </div>
</template>

<script setup lang="ts">
import type { TaskDetailResponse } from '../api/ronflowApi'

defineProps<{
  isOpen: boolean
  task: TaskDetailResponse | null
  formatTimelineTime: (occurredAt: string) => string
}>()

defineEmits<{
  (event: 'close'): void
}>()
</script>