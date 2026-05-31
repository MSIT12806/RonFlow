<template>
  <section class="board-panel">
    <header class="board-header">
      <div>
        <p class="eyebrow">Project traceability</p>
        <h2 class="board-title">程式修改紀錄</h2>
      </div>

      <button type="button" class="secondary-button" @click="$emit('back-to-board')">
        返回看板
      </button>
    </header>

    <AsyncStateBoundary
      :is-loading="isLoading"
      :error-message="errorMessage"
      loading-message="正在載入程式修改紀錄..."
    >
      <div class="traceability-filter-bar">
        <label>
          紀錄類別
          <select v-model="categoryFilter" aria-label="紀錄類別">
            <option value="all">全部</option>
            <option value="api">API</option>
            <option value="frontendPages">前端頁面</option>
            <option value="frontendComponents">前端元件</option>
          </select>
        </label>

        <label>
          變更類型
          <select v-model="changeTypeFilter" aria-label="變更類型">
            <option value="all">全部</option>
            <option value="added">新增</option>
            <option value="modified">修改</option>
            <option value="removed">移除</option>
          </select>
        </label>

        <label>
          關鍵字
          <input v-model="keyword" type="search" aria-label="關鍵字" placeholder="搜尋 task 或 target" />
        </label>
      </div>

      <div v-if="filteredItems.length === 0" class="board-empty-state traceability-empty-state">
        <p class="eyebrow">No traceability records</p>
        <p class="empty-copy">目前沒有符合條件的程式修改紀錄。</p>
      </div>

      <div v-else class="traceability-result-list">
        <article
          v-for="(item, index) in filteredItems"
          :key="`${item.taskId}-${item.category}-${item.changeType}-${item.target}`"
          class="lifecycle-task-card"
          data-testid="code-traceability-result"
        >
          <div class="lifecycle-task-copy">
            <strong v-if="shouldShowTaskTitle(index)">{{ item.taskTitle }}</strong>
            <span v-else class="task-meta">同一任務</span>
            <span class="task-meta">{{ formatCategory(item.category) }}</span>
          </div>

          <dl class="lifecycle-task-details traceability-result-details">
            <div>
              <dt>變更類型</dt>
              <dd>{{ formatChangeType(item.changeType) }}</dd>
            </div>
            <div>
              <dt>Target</dt>
              <dd>{{ item.target }}</dd>
            </div>
          </dl>
        </article>
      </div>
    </AsyncStateBoundary>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import AsyncStateBoundary from './bases/AsyncStateBoundary.vue'
import type {
  ProjectCodeTraceabilityCategory,
  ProjectCodeTraceabilityItemResponse,
  TaskCodeTraceabilityChangeType,
} from '../api/ronflowApi'

const props = defineProps<{
  items: ProjectCodeTraceabilityItemResponse[]
  isLoading: boolean
  errorMessage: string
}>()

defineEmits<{
  (event: 'back-to-board'): void
}>()

const categoryFilter = ref<ProjectCodeTraceabilityCategory | 'all'>('all')
const changeTypeFilter = ref<TaskCodeTraceabilityChangeType | 'all'>('all')
const keyword = ref('')

const filteredItems = computed(() => {
  const normalizedKeyword = keyword.value.trim().toLocaleLowerCase()

  return props.items.filter((item) => {
    const categoryMatches = categoryFilter.value === 'all' || item.category === categoryFilter.value
    const changeTypeMatches = changeTypeFilter.value === 'all' || item.changeType === changeTypeFilter.value
    const keywordMatches = normalizedKeyword.length === 0
      || item.taskTitle.toLocaleLowerCase().includes(normalizedKeyword)
      || item.target.toLocaleLowerCase().includes(normalizedKeyword)

    return categoryMatches && changeTypeMatches && keywordMatches
  })
})

function shouldShowTaskTitle(index: number): boolean {
  return index === 0 || filteredItems.value[index - 1].taskId !== filteredItems.value[index].taskId
}

function formatCategory(category: ProjectCodeTraceabilityCategory): string {
  switch (category) {
    case 'api':
      return 'API'
    case 'frontendPages':
      return '前端頁面'
    case 'frontendComponents':
      return '前端元件'
  }
}

function formatChangeType(changeType: TaskCodeTraceabilityChangeType): string {
  switch (changeType) {
    case 'added':
      return '新增'
    case 'removed':
      return '移除'
    default:
      return '修改'
  }
}
</script>