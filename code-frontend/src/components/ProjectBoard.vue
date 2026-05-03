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

      <p v-if="isLoadingBoard" class="empty-copy">正在載入專案看板...</p>

      <div v-else class="board-grid">
        <article
          v-for="column in columns"
          :key="column.stateKey"
          :data-testid="`workflow-column-${column.stateKey}`"
          class="board-column"
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
            >
              <button
                type="button"
                class="task-card-main"
                @click="$emit('open-task-detail', task.id)"
              >
                <span class="task-title">{{ task.title }}</span>
                <span class="task-meta">{{ column.label }}</span>
              </button>

              <button
                v-for="targetState in moveTargets(column.stateKey)"
                :key="targetState.stateKey"
                type="button"
                class="secondary-button task-state-action"
                @click="$emit('move-task-to-state', task.id, targetState.stateKey)"
              >
                移到{{ targetState.label }}
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
import type { BoardColumnResponse, WorkflowKey } from '../api/ronflowApi'

const props = defineProps<{
  activeProjectName: string | null
  columns: BoardColumnResponse[]
  isLoadingBoard: boolean
}>()

defineEmits<{
  (event: 'open-create-task'): void
  (event: 'open-task-detail', taskId: string): void
  (event: 'move-task-to-state', taskId: string, stateKey: WorkflowKey): void
}>()

function moveTargets(currentStateKey: WorkflowKey) {
  return props.columns
    .filter((column) => column.stateKey !== currentStateKey)
    .map((column) => ({ stateKey: column.stateKey, label: column.label }))
}
</script>