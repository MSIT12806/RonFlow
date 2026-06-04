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
      <button type="button" class="secondary-button" :class="{ 'tab-active': activeTab === 'workflow' }" @click="setActiveTab('workflow')">
        工作流量
      </button>
      <button type="button" class="secondary-button" :class="{ 'tab-active': activeTab === 'aging' }" @click="setActiveTab('aging')">
        任務停留
      </button>
      <button type="button" class="secondary-button" :class="{ 'tab-active': activeTab === 'cycle' }" @click="setActiveTab('cycle')">
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

    <section v-else-if="activeTab === 'aging'" class="reports-section">
      <div class="traceability-filter-bar">
        <label>
          Todo 閾值（天）
          <input :value="localThresholds.todoThresholdDays" type="number" min="0" aria-label="Todo 閾值（天）" @input="onThresholdChange('todoThresholdDays', $event)" />
        </label>

        <label>
          Active 閾值（天）
          <input :value="localThresholds.activeThresholdDays" type="number" min="0" aria-label="Active 閾值（天）" @input="onThresholdChange('activeThresholdDays', $event)" />
        </label>

        <label>
          Review 閾值（天）
          <input :value="localThresholds.reviewThresholdDays" type="number" min="0" aria-label="Review 閾值（天）" @input="onThresholdChange('reviewThresholdDays', $event)" />
        </label>

        <span class="task-meta">最後更新時間：{{ formatLastUpdatedAt(agingReport?.lastUpdatedAt ?? null) }}</span>
      </div>

      <div v-if="agingReport?.thresholds.length" class="traceability-filter-bar">
        <span v-for="threshold in agingReport.thresholds" :key="threshold.stateKey" class="task-meta">
          {{ threshold.stateKey }} 超過 {{ threshold.thresholdDays }} 天
        </span>
      </div>

      <AsyncStateBoundary
        :is-loading="isLoadingAging"
        :error-message="agingErrorMessage"
        loading-message="正在載入任務停留報表..."
      >
        <div v-if="!agingReport || agingReport.items.length === 0" class="board-empty-state">
          <p class="eyebrow">Task Aging</p>
          <p class="empty-copy">目前沒有超過停留閾值的任務。</p>
        </div>

        <div v-else class="traceability-result-list reports-result-list">
          <button
            v-for="item in agingReport.items"
            :key="item.taskId"
            type="button"
            class="lifecycle-task-card reports-bucket-card reports-aging-item"
            data-testid="task-aging-item"
            @click="$emit('open-task-detail', item.taskId, item.title)"
          >
            <div class="lifecycle-task-copy">
              <strong>{{ item.title }}</strong>
              <span class="task-meta">{{ item.currentState.label }}</span>
            </div>

            <dl class="lifecycle-task-details traceability-result-details reports-bucket-details">
              <div><dt>目前狀態</dt><dd>{{ item.currentState.label }}</dd></div>
              <div><dt>停留天數</dt><dd>{{ item.agingDays }}</dd></div>
              <div><dt>進入時間</dt><dd>{{ formatLastUpdatedAt(item.enteredStateAt) }}</dd></div>
            </dl>
          </button>
        </div>
      </AsyncStateBoundary>
    </section>

    <section v-else class="reports-section">
      <div class="traceability-filter-bar">
        <label>
          完成起日
          <input :value="localCycleRange.completedFrom" type="date" aria-label="完成起日" @input="onCycleRangeChange('completedFrom', $event)" />
        </label>

        <label>
          完成迄日
          <input :value="localCycleRange.completedTo" type="date" aria-label="完成迄日" @input="onCycleRangeChange('completedTo', $event)" />
        </label>

        <span class="task-meta">最後更新時間：{{ formatLastUpdatedAt(cycleReport?.lastUpdatedAt ?? null) }}</span>
      </div>

      <AsyncStateBoundary
        :is-loading="isLoadingCycle"
        :error-message="cycleErrorMessage"
        loading-message="正在載入週期時間報表..."
      >
        <div v-if="!cycleReport || isCycleReportEmpty" class="board-empty-state reports-placeholder-state">
          <p class="eyebrow">Cycle Time / Lead Time</p>
          <h3>目前沒有落在此區間的已完成任務。</h3>
          <p class="empty-copy">完成任務後，這裡會顯示 task 從建立到完成，以及從進入 Active 到 Done 的統計結果。</p>
        </div>

        <div v-else class="traceability-result-list reports-result-list">
          <article class="lifecycle-task-card reports-bucket-card" data-testid="cycle-time-lead-time-card">
            <div class="lifecycle-task-copy">
              <strong>Lead Time</strong>
              <span class="task-meta">樣本數 {{ cycleReport.leadTime.sampleCount }}</span>
            </div>

            <dl class="lifecycle-task-details traceability-result-details reports-bucket-details">
              <div><dt>平均值</dt><dd>{{ formatDuration(cycleReport.leadTime.averageHours) }}</dd></div>
              <div><dt>中位數</dt><dd>{{ formatDuration(cycleReport.leadTime.medianHours) }}</dd></div>
              <div><dt>p90</dt><dd>{{ formatDuration(cycleReport.leadTime.p90Hours) }}</dd></div>
              <div><dt>區間</dt><dd>{{ cycleReport.completedFrom }} ~ {{ cycleReport.completedTo }}</dd></div>
            </dl>
          </article>

          <article class="lifecycle-task-card reports-bucket-card" data-testid="cycle-time-cycle-time-card">
            <div class="lifecycle-task-copy">
              <strong>Cycle Time</strong>
              <span class="task-meta">樣本數 {{ cycleReport.cycleTime.sampleCount }}</span>
            </div>

            <dl class="lifecycle-task-details traceability-result-details reports-bucket-details">
              <div><dt>平均值</dt><dd>{{ formatDuration(cycleReport.cycleTime.averageHours) }}</dd></div>
              <div><dt>中位數</dt><dd>{{ formatDuration(cycleReport.cycleTime.medianHours) }}</dd></div>
              <div><dt>p90</dt><dd>{{ formatDuration(cycleReport.cycleTime.p90Hours) }}</dd></div>
              <div><dt>說明</dt><dd>僅計算曾進入 Active 並最終完成的任務</dd></div>
            </dl>
          </article>
        </div>
      </AsyncStateBoundary>
    </section>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import AsyncStateBoundary from './bases/AsyncStateBoundary.vue'
import type { CycleTimeReportResponse, TaskAgingReportResponse, WorkflowThroughputReportResponse } from '../api/ronflowApi'

const props = defineProps<{
  activeProjectName: string | null
  report: WorkflowThroughputReportResponse | null
  agingReport: TaskAgingReportResponse | null
  cycleReport: CycleTimeReportResponse | null
  agingThresholds: {
    todoThresholdDays: number
    activeThresholdDays: number
    reviewThresholdDays: number
  }
  cycleRange: {
    completedFrom: string
    completedTo: string
  }
  bucketType: 'day' | 'week'
  isLoading: boolean
  isLoadingAging: boolean
  isLoadingCycle: boolean
  errorMessage: string
  agingErrorMessage: string
  cycleErrorMessage: string
}>()

const emit = defineEmits<{
  (event: 'back-to-board'): void
  (event: 'change-bucket', bucket: 'day' | 'week'): void
  (event: 'change-task-aging-thresholds', thresholds: { todoThresholdDays: number; activeThresholdDays: number; reviewThresholdDays: number }): void
  (event: 'change-cycle-range', range: { completedFrom: string; completedTo: string }): void
  (event: 'open-task-detail', taskId: string, taskTitle: string): void
}>()

const activeTab = ref<'workflow' | 'aging' | 'cycle'>('workflow')
const localThresholds = ref({ ...props.agingThresholds })
const localCycleRange = ref({ ...props.cycleRange })

watch(() => props.agingThresholds, (nextValue) => {
  localThresholds.value = { ...nextValue }
}, { deep: true })

watch(() => props.cycleRange, (nextValue) => {
  localCycleRange.value = { ...nextValue }
}, { deep: true })

const formattedLastUpdatedAt = computed(() => formatLastUpdatedAt(props.report?.lastUpdatedAt ?? null))
const isCycleReportEmpty = computed(() => {
  if (!props.cycleReport) {
    return true
  }

  return props.cycleReport.leadTime.sampleCount === 0 && props.cycleReport.cycleTime.sampleCount === 0
})

function formatLastUpdatedAt(value: string | null) {
  if (!value) {
    return '尚未更新'
  }

  return new Intl.DateTimeFormat('zh-TW', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

function setActiveTab(tab: 'workflow' | 'aging' | 'cycle') {
  activeTab.value = tab
}

function onBucketChange(event: Event) {
  const nextBucket = (event.target as HTMLSelectElement).value === 'week' ? 'week' : 'day'
  emit('change-bucket', nextBucket)
}

function onThresholdChange(
  key: 'todoThresholdDays' | 'activeThresholdDays' | 'reviewThresholdDays',
  event: Event,
) {
  const rawValue = Number.parseInt((event.target as HTMLInputElement).value, 10)
  const nextValue = Number.isNaN(rawValue) || rawValue < 0 ? 0 : rawValue

  localThresholds.value = {
    ...localThresholds.value,
    [key]: nextValue,
  }

  emit('change-task-aging-thresholds', { ...localThresholds.value })
}

function onCycleRangeChange(key: 'completedFrom' | 'completedTo', event: Event) {
  localCycleRange.value = {
    ...localCycleRange.value,
    [key]: (event.target as HTMLInputElement).value,
  }

  emit('change-cycle-range', { ...localCycleRange.value })
}

function formatBucketStart(value: string) {
  return props.bucketType === 'day'
    ? `${value}`
    : `${value} 起`
}

function formatDuration(value: number | null) {
  if (value === null) {
    return '資料不足'
  }

  return `${value.toFixed(1)} 小時`
}
</script>