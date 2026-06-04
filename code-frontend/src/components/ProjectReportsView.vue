<template>
  <section class="board-panel">
    <header class="board-header">
      <div>
        <p class="eyebrow">Project reports</p>
        <h2 class="board-title">專案報表</h2>
        <p class="board-description">{{ activeProjectName ? `${activeProjectName} 的統計摘要` : '查看目前專案的報表與統計' }}</p>
      </div>

      <button type="button" class="secondary-button" @click="$emit('back-to-board')">
        回到看板
      </button>
    </header>

    <div class="traceability-filter-bar reports-tab-bar" role="tablist" aria-label="報表切換">
      <button type="button" class="secondary-button" :class="{ 'tab-active': activeTab === 'workflow' }" @click="activeTab = 'workflow'">
        工作流量
      </button>
      <button type="button" class="secondary-button" :class="{ 'tab-active': activeTab === 'aging' }" @click="activeTab = 'aging'">
        任務停留
      </button>
      <button type="button" class="secondary-button" :class="{ 'tab-active': activeTab === 'cycle' }" @click="activeTab = 'cycle'">
        週期時間
      </button>
    </div>

    <section v-if="activeTab === 'workflow'" class="reports-section">
      <div class="traceability-filter-bar">
        <label>
          統計粒度
          <select :value="bucketType" aria-label="統計粒度" @change="onBucketChange">
            <option value="day">每日</option>
            <option value="week">每週</option>
          </select>
        </label>

        <span class="task-meta">最後更新時間：{{ formattedLastUpdatedAt }}</span>
      </div>

      <AsyncStateBoundary
        :is-loading="isLoading"
        :error-message="errorMessage"
        loading-message="正在載入工作流量報表..."
      >
        <div v-if="!report || report.buckets.length === 0" class="board-empty-state">
          <p class="eyebrow">No report data</p>
          <p class="empty-copy">目前尚無報表資料。</p>
        </div>

        <div v-else class="traceability-result-list reports-result-list">
          <article
            v-for="bucket in report.buckets"
            :key="bucket.bucketStart"
            class="lifecycle-task-card reports-bucket-card"
            data-testid="workflow-throughput-bucket"
          >
            <div class="lifecycle-task-copy">
              <strong>{{ formatBucketStart(bucket.bucketStart) }}</strong>
              <span class="task-meta">{{ bucketType === 'day' ? '每日統計' : '每週統計' }}</span>
            </div>

            <dl class="lifecycle-task-details traceability-result-details reports-bucket-details">
              <div><dt>建立</dt><dd data-testid="throughput-created-count">{{ bucket.createdCount }}</dd></div>
              <div><dt>進行中</dt><dd data-testid="throughput-active-count">{{ bucket.movedToActiveCount }}</dd></div>
              <div><dt>審查中</dt><dd data-testid="throughput-review-count">{{ bucket.movedToReviewCount }}</dd></div>
              <div><dt>已完成</dt><dd data-testid="throughput-completed-count">{{ bucket.completedCount }}</dd></div>
              <div><dt>重新開啟</dt><dd data-testid="throughput-reopened-count">{{ bucket.reopenedCount }}</dd></div>
            </dl>
          </article>
        </div>
      </AsyncStateBoundary>
    </section>

    <section v-else-if="activeTab === 'aging'" class="board-empty-state reports-placeholder-state">
      <p class="eyebrow">Task Aging</p>
      <h3>任務停留報表將在下一批交付。</h3>
      <p class="empty-copy">這裡會聚焦目前卡在特定 workflow state 太久的任務。</p>
    </section>

    <section v-else class="board-empty-state reports-placeholder-state">
      <p class="eyebrow">Cycle Time / Lead Time</p>
      <h3>週期時間報表將在下一批交付。</h3>
      <p class="empty-copy">這裡會顯示 task 從建立到完成、或從進入 Active 到 Done 的統計結果。</p>
    </section>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import AsyncStateBoundary from './bases/AsyncStateBoundary.vue'
import type { WorkflowThroughputReportResponse } from '../api/ronflowApi'

const props = defineProps<{
  activeProjectName: string | null
  report: WorkflowThroughputReportResponse | null
  bucketType: 'day' | 'week'
  isLoading: boolean
  errorMessage: string
}>()

const emit = defineEmits<{
  (event: 'back-to-board'): void
  (event: 'change-bucket', bucket: 'day' | 'week'): void
}>()

const activeTab = ref<'workflow' | 'aging' | 'cycle'>('workflow')

const formattedLastUpdatedAt = computed(() => {
  if (!props.report?.lastUpdatedAt) {
    return '尚未更新'
  }

  return new Intl.DateTimeFormat('zh-TW', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(props.report.lastUpdatedAt))
})

function onBucketChange(event: Event) {
  const nextBucket = (event.target as HTMLSelectElement).value === 'week' ? 'week' : 'day'
  emit('change-bucket', nextBucket)
}

function formatBucketStart(value: string) {
  return props.bucketType === 'day'
    ? `${value}`
    : `${value} 起`
}
</script>