<template>
  <section class="board-panel">
    <template v-if="activeProjectName">
      <header class="board-header">
        <div>
          <p class="eyebrow">Current board</p>
          <h2 class="board-title">{{ activeProjectName }}</h2>
        </div>

        <button type="button" class="primary-button" @click="$emit('open-create-task')">
          建立任務
        </button>
      </header>

      <BaseLoadingState v-if="isLoadingBoard" message="正在載入專案看板..." />

      <div v-else class="board-grid">
        <article
          v-for="column in columns"
          :key="column.stateKey"
          :data-testid="`workflow-column-${column.stateKey}`"
          class="board-column"
          :class="{ 'board-column-drop-target': dragOverStateKey === column.stateKey }"
          @dragenter.prevent="handleColumnDragEnter(column.stateKey)"
          @dragover.prevent="handleColumnDragOver($event, column.stateKey)"
          @dragleave="handleColumnDragLeave(column.stateKey)"
          @drop.prevent="handleTaskDrop($event, column.stateKey)"
        >
          <header class="column-header">
            <h3>{{ column.label }}</h3>
            <span class="count-badge">{{ column.tasks.length }}</span>
          </header>

          <div v-if="column.tasks.length === 0" class="column-empty">
            {{ column.emptyStateMessage }}
          </div>

          <div v-else class="task-list">
            <article
              v-for="task in column.tasks"
              :key="task.id"
              class="task-card"
              draggable="true"
              @dragstart="handleTaskDragStart($event, task.id)"
              @dragend="handleTaskDragEnd"
            >
              <button
                type="button"
                class="task-card-main"
                @click="$emit('open-task-detail', task.id)"
              >
                <span class="task-title">{{ task.title }}</span>
                <span class="task-meta">{{ column.label }}</span>
              </button>
            </article>
          </div>
        </article>
      </div>
    </template>

    <div v-else class="board-empty-state">
      <p class="eyebrow">Outside-In slice</p>
      <h2>先建立第一個專案，再展開任務看板。</h2>
      <p>
        現在會直接向後端讀取專案與任務資料，建立後也會同步刷新看板狀態。
      </p>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import BaseLoadingState from './bases/BaseLoadingState.vue'
import type { BoardColumnResponse, WorkflowKey } from '../api/ronflowApi'

const props = defineProps<{
  activeProjectName: string | null
  columns: BoardColumnResponse[]
  isLoadingBoard: boolean
}>()

const emit = defineEmits<{
  (event: 'open-create-task'): void
  (event: 'open-task-detail', taskId: string): void
  (event: 'move-task-to-state', taskId: string, stateKey: WorkflowKey): void
}>()

const draggingTaskId = ref<string | null>(null)
const dragOverStateKey = ref<WorkflowKey | null>(null)

function handleTaskDragStart(event: DragEvent, taskId: string) {
  draggingTaskId.value = taskId

  if (!event.dataTransfer) {
    return
  }

  event.dataTransfer.effectAllowed = 'move'
  event.dataTransfer.setData('text/plain', taskId)
}

function handleTaskDragEnd() {
  draggingTaskId.value = null
  dragOverStateKey.value = null
}

function handleColumnDragEnter(stateKey: WorkflowKey) {
  if (!draggingTaskId.value) {
    return
  }

  dragOverStateKey.value = stateKey
}

function handleColumnDragOver(event: DragEvent, stateKey: WorkflowKey) {
  if (!draggingTaskId.value) {
    return
  }

  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move'
  }

  dragOverStateKey.value = stateKey
}

function handleColumnDragLeave(stateKey: WorkflowKey) {
  if (dragOverStateKey.value !== stateKey) {
    return
  }

  dragOverStateKey.value = null
}

function handleTaskDrop(event: DragEvent, targetStateKey: WorkflowKey) {
  const taskId = draggingTaskId.value ?? event.dataTransfer?.getData('text/plain') ?? null
  if (!taskId) {
    return
  }

  const sourceColumn = props.columns.find((column) => column.tasks.some((task) => task.id === taskId))
  dragOverStateKey.value = null
  draggingTaskId.value = null

  if (!sourceColumn || sourceColumn.stateKey === targetStateKey) {
    return
  }

  emit('move-task-to-state', taskId, targetStateKey)
}
</script>