<template>
  <li
    class="task-tree-node"
    :class="{
      'task-tree-node-drop-before': isDropBefore,
      'task-tree-node-drop-after': isDropAfter,
    }"
    :style="{ '--task-tree-depth': String(depth) }"
  >
    <div class="task-tree-row">
      <button
        v-if="hasChildren"
        type="button"
        class="task-tree-toggle"
        :aria-expanded="isExpanded"
        :aria-label="isExpanded ? '收合 child tasks' : '展開 child tasks'"
        @click="isExpanded = !isExpanded"
      >
        {{ isExpanded ? '-' : '+' }}
      </button>
      <span v-else class="task-tree-spacer"></span>

      <button
        :data-testid="`task-tree-item-${task.id}`"
        type="button"
        class="task-tree-item"
        :class="{
          'task-tree-item-completed': task.isCompleted,
          'task-tree-item-in-flow': task.isInFlow,
          'task-tree-item-split-complete': task.isSplitComplete,
          'task-tree-item-selected': isSelected,
          'task-tree-item-drop-inside': isDropInside,
        }"
        draggable="true"
        @click="$emit('select-task', task.id)"
        @dragstart="$emit('task-drag-start', $event, task.id)"
        @dragend="$emit('task-drag-end')"
        @dragenter.prevent="handleDragOver"
        @dragover.prevent="handleDragOver"
        @dragleave="handleDragLeave"
        @drop.prevent="handleDrop"
      >
        <span class="task-tree-item-title-row">
          <span
            class="task-tree-completion-indicator"
            :class="{
              'task-tree-completion-indicator-todo': taskStatus === 'todo',
              'task-tree-completion-indicator-doing': taskStatus === 'doing',
              'task-tree-completion-indicator-done': taskStatus === 'completed',
            }"
            aria-hidden="true"
          >
            {{ taskStatusIcon }}
          </span>
          <span class="task-title">
            {{ task.title }}
            <span v-if="task.isInFlow" class="task-tree-flow-badge">In Flow</span>
            <span
              v-if="task.isSplitComplete"
              class="task-status-badge task-status-badge-split-complete"
              data-testid="task-split-complete-badge"
            >
              拆解完成
            </span>
          </span>
        </span>
        <span v-if="hasChildren" class="task-tree-child-status-summary" aria-label="子任務狀態統計">
          <span class="task-tree-child-status" data-testid="task-tree-child-status-todo" title="Todo">
            <span aria-hidden="true">○</span>
            {{ childStatusSummary.todo }}
          </span>
          <span class="task-tree-child-status" data-testid="task-tree-child-status-doing" title="Doing">
            <span aria-hidden="true">◐</span>
            {{ childStatusSummary.doing }}
          </span>
          <span class="task-tree-child-status" data-testid="task-tree-child-status-completed" title="Completed">
            <span aria-hidden="true">✓</span>
            {{ childStatusSummary.completed }}
          </span>
        </span>
        <span v-else class="task-meta">{{ nodeMeta }}</span>
      </button>
      <button
        type="button"
        class="task-tree-open-detail-button"
        aria-label="展開任務詳細資訊"
        @click="$emit('open-task-detail', task.id, task.title)"
      >
        展開
      </button>
    </div>

    <ul v-if="hasChildren && isExpanded" class="task-tree-children">
      <TaskTreeNode
        v-for="child in task.children"
        :key="child.id"
        :task="child"
        :depth="depth + 1"
        :parent-task-id="task.id"
        :selected-task-id="selectedTaskId"
        :dragging-task-id="draggingTaskId"
        :active-drop-target="activeDropTarget"
        @open-task-detail="(taskId, taskTitle) => $emit('open-task-detail', taskId, taskTitle)"
        @select-task="(taskId) => $emit('select-task', taskId)"
        @task-drag-start="(event, taskId) => $emit('task-drag-start', event, taskId)"
        @task-drag-end="$emit('task-drag-end')"
        @task-drag-over="(payload) => $emit('task-drag-over', payload)"
        @task-drag-leave="(taskId) => $emit('task-drag-leave', taskId)"
        @task-drop="(payload) => $emit('task-drop', payload)"
      />
    </ul>
  </li>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { BoardTaskCardResponse } from '../api/ronflowApi'

type TaskTreeDropPlacement = 'before' | 'after' | 'inside'

const props = withDefaults(defineProps<{
  task: BoardTaskCardResponse
  depth?: number
  parentTaskId?: string | null
  selectedTaskId?: string | null
  draggingTaskId?: string | null
  activeDropTarget?: { taskId: string; placement: TaskTreeDropPlacement } | null
}>(), {
  depth: 0,
  parentTaskId: null,
  selectedTaskId: null,
  draggingTaskId: null,
  activeDropTarget: null,
})

const emit = defineEmits<{
  (event: 'open-task-detail', taskId: string, taskTitle: string): void
  (event: 'select-task', taskId: string): void
  (event: 'task-drag-start', dragEvent: DragEvent, taskId: string): void
  (event: 'task-drag-end'): void
  (event: 'task-drag-over', payload: { taskId: string; parentTaskId: string | null; placement: TaskTreeDropPlacement }): void
  (event: 'task-drag-leave', taskId: string): void
  (event: 'task-drop', payload: { taskId: string; parentTaskId: string | null; placement: TaskTreeDropPlacement }): void
}>()

type TaskTreeStatus = 'todo' | 'doing' | 'completed'

const isExpanded = ref(!props.task.isCompleted)
const hasChildren = computed(() => props.task.children.length > 0)
const isSelected = computed(() => props.selectedTaskId === props.task.id)
const isDropBefore = computed(() => props.activeDropTarget?.taskId === props.task.id && props.activeDropTarget.placement === 'before')
const isDropAfter = computed(() => props.activeDropTarget?.taskId === props.task.id && props.activeDropTarget.placement === 'after')
const isDropInside = computed(() => props.activeDropTarget?.taskId === props.task.id && props.activeDropTarget.placement === 'inside')
const taskStatus = computed<TaskTreeStatus>(() => getTaskTreeStatus(props.task))
const taskStatusIcon = computed(() => {
  switch (taskStatus.value) {
    case 'completed':
      return '✓'
    case 'doing':
      return '◐'
    default:
      return '○'
  }
})
const childStatusSummary = computed(() => props.task.children.reduce(
  (summary, childTask) => {
    summary[getTaskTreeStatus(childTask)] += 1
    return summary
  },
  { todo: 0, doing: 0, completed: 0 },
))

const nodeMeta = computed(() => {
  if (props.task.isInFlow) {
    return 'Flow task'
  }

  if (!hasChildren.value) {
    return 'Leaf task'
  }

  return 'Parent task'
})

function getTaskTreeStatus(task: BoardTaskCardResponse): TaskTreeStatus {
  if (task.isCompleted) {
    return 'completed'
  }

  return task.isInFlow ? 'doing' : 'todo'
}

function resolveDropPlacement(event: DragEvent): TaskTreeDropPlacement {
  const currentTarget = event.currentTarget
  if (!(currentTarget instanceof HTMLElement)) {
    return 'inside'
  }

  const bounds = currentTarget.getBoundingClientRect()
  const offsetY = event.clientY - bounds.top
  const edgeHeight = Math.max(bounds.height * 0.28, 12)

  if (offsetY <= edgeHeight) {
    return 'before'
  }

  if (offsetY >= bounds.height - edgeHeight) {
    return 'after'
  }

  return 'inside'
}

function handleDragOver(event: DragEvent) {
  if (!props.draggingTaskId || props.draggingTaskId === props.task.id) {
    return
  }

  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move'
  }

  emit('task-drag-over', {
    taskId: props.task.id,
    parentTaskId: props.parentTaskId,
    placement: resolveDropPlacement(event),
  })
}

function handleDragLeave(event: DragEvent) {
  const nextTarget = event.relatedTarget
  if (nextTarget instanceof Node && event.currentTarget instanceof Node && event.currentTarget.contains(nextTarget)) {
    return
  }

  emit('task-drag-leave', props.task.id)
}

function handleDrop(event: DragEvent) {
  if (!props.draggingTaskId || props.draggingTaskId === props.task.id) {
    emit('task-drag-end')
    return
  }

  emit('task-drop', {
    taskId: props.task.id,
    parentTaskId: props.parentTaskId,
    placement: resolveDropPlacement(event),
  })
}
</script>
