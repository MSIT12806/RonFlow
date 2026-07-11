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
          'task-tree-item-drop-inside': isDropInside,
        }"
        draggable="true"
        @click="$emit('open-task-detail', task.id, task.title)"
        @dragstart="$emit('task-drag-start', $event, task.id)"
        @dragend="$emit('task-drag-end')"
        @dragenter.prevent="handleDragOver"
        @dragover.prevent="handleDragOver"
        @dragleave="handleDragLeave"
        @drop.prevent="handleDrop"
      >
        <span class="task-tree-item-title-row">
          <span class="task-tree-completion-indicator" :class="{ 'task-tree-completion-indicator-done': task.isCompleted }" aria-hidden="true">
            {{ task.isCompleted ? '✓' : '' }}
          </span>
          <span class="task-title">
            {{ task.title }}
            <span v-if="task.isInFlow" class="task-tree-flow-badge">In Flow</span>
          </span>
        </span>
        <span class="task-meta">{{ nodeMeta }}</span>
      </button>
    </div>

    <ul v-if="hasChildren && isExpanded" class="task-tree-children">
      <TaskTreeNode
        v-for="child in task.children"
        :key="child.id"
        :task="child"
        :depth="depth + 1"
        :parent-task-id="task.id"
        :dragging-task-id="draggingTaskId"
        :active-drop-target="activeDropTarget"
        @open-task-detail="(taskId, taskTitle) => $emit('open-task-detail', taskId, taskTitle)"
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
  draggingTaskId?: string | null
  activeDropTarget?: { taskId: string; placement: TaskTreeDropPlacement } | null
}>(), {
  depth: 0,
  parentTaskId: null,
  draggingTaskId: null,
  activeDropTarget: null,
})

const emit = defineEmits<{
  (event: 'open-task-detail', taskId: string, taskTitle: string): void
  (event: 'task-drag-start', dragEvent: DragEvent, taskId: string): void
  (event: 'task-drag-end'): void
  (event: 'task-drag-over', payload: { taskId: string; parentTaskId: string | null; placement: TaskTreeDropPlacement }): void
  (event: 'task-drag-leave', taskId: string): void
  (event: 'task-drop', payload: { taskId: string; parentTaskId: string | null; placement: TaskTreeDropPlacement }): void
}>()

const isExpanded = ref(true)
const hasChildren = computed(() => props.task.children.length > 0)
const isDropBefore = computed(() => props.activeDropTarget?.taskId === props.task.id && props.activeDropTarget.placement === 'before')
const isDropAfter = computed(() => props.activeDropTarget?.taskId === props.task.id && props.activeDropTarget.placement === 'after')
const isDropInside = computed(() => props.activeDropTarget?.taskId === props.task.id && props.activeDropTarget.placement === 'inside')

const nodeMeta = computed(() => {
  if (props.task.isInFlow) {
    return 'Flow task'
  }

  if (!hasChildren.value) {
    return 'Leaf task'
  }

  return `Parent task · ${props.task.children.length} child${props.task.children.length === 1 ? '' : 'ren'}`
})

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
