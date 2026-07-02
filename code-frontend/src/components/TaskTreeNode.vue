<template>
  <li class="task-tree-node" :style="{ '--task-tree-depth': String(depth) }">
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
        type="button"
        class="task-tree-item"
        @click="$emit('open-task-detail', task.id, task.title)"
      >
        <span class="task-title">{{ task.title }}</span>
        <span class="task-meta">{{ nodeMeta }}</span>
      </button>
    </div>

    <ul v-if="hasChildren && isExpanded" class="task-tree-children">
      <TaskTreeNode
        v-for="child in task.children"
        :key="child.id"
        :task="child"
        :depth="depth + 1"
        @open-task-detail="(taskId, taskTitle) => $emit('open-task-detail', taskId, taskTitle)"
      />
    </ul>
  </li>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { BoardTaskCardResponse } from '../api/ronflowApi'

const props = withDefaults(defineProps<{
  task: BoardTaskCardResponse
  depth?: number
}>(), {
  depth: 0,
})

defineEmits<{
  (event: 'open-task-detail', taskId: string, taskTitle: string): void
}>()

const isExpanded = ref(true)
const hasChildren = computed(() => props.task.children.length > 0)
const nodeMeta = computed(() => {
  if (!hasChildren.value) {
    return 'Leaf task'
  }

  return `Parent task · ${props.task.children.length} child${props.task.children.length === 1 ? '' : 'ren'}`
})
</script>
