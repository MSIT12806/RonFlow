<template>
  <main class="playground-shell">
    <section class="playground-card">
      <header class="playground-header">
        <div>
          <p class="eyebrow">CommonSpec playground</p>
          <h1>Async state primitives</h1>
          <p class="playground-copy">
            使用這個 dev-only playground 可以穩定人工檢查 loading / error 呈現，不需要等待真實 API 剛好失敗。
          </p>
        </div>

        <a class="secondary-button playground-link" href="/">回到 RonFlow</a>
      </header>

      <div class="playground-toolbar" role="tablist" aria-label="選擇 async state 範例">
        <button
          v-for="scenario in scenarios"
          :key="scenario.id"
          type="button"
          class="playground-toggle"
          :class="{ 'playground-toggle-active': scenario.id === activeScenarioId }"
          :aria-pressed="scenario.id === activeScenarioId"
          @click="activeScenarioId = scenario.id"
        >
          {{ scenario.label }}
        </button>
      </div>

      <section class="playground-preview">
        <div>
          <p class="detail-label">目前情境</p>
          <h2>{{ activeScenario.label }}</h2>
          <p class="playground-copy">{{ activeScenario.description }}</p>
        </div>

        <BaseLoadingState
          v-if="activeScenario.type === 'loading'"
          :message="activeScenario.message"
        />

        <BaseErrorState
          v-else
          :message="activeScenario.message"
          :scope="activeScenario.scope"
        />
      </section>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import BaseErrorState from './BaseErrorState.vue'
import BaseLoadingState from './BaseLoadingState.vue'

type LoadingScenario = {
  id: string
  label: string
  description: string
  type: 'loading'
  message: string
}

type ErrorScenario = {
  id: string
  label: string
  description: string
  type: 'error'
  message: string
  scope: 'page' | 'section'
}

type PlaygroundScenario = LoadingScenario | ErrorScenario

const scenarios: PlaygroundScenario[] = [
  {
    id: 'project-list-loading',
    label: 'Project list loading',
    description: '模擬專案列表尚未載入完成時的 section-level loading。',
    type: 'loading',
    message: '正在載入專案列表...',
  },
  {
    id: 'board-loading',
    label: 'Board loading',
    description: '模擬看板資料尚未載入完成時的 section-level loading。',
    type: 'loading',
    message: '正在載入專案看板...',
  },
  {
    id: 'task-detail-loading',
    label: 'Task detail loading',
    description: '模擬 task detail 尚未載入完成時的區塊 loading 呈現。',
    type: 'loading',
    message: '正在載入任務詳細資訊...',
  },
  {
    id: 'page-error',
    label: 'Page error',
    description: '模擬 page-level 資料讀取失敗，應使用 page scope error state。',
    type: 'error',
    message: '無法載入專案列表，請確認後端 API 已啟動。',
    scope: 'page',
  },
  {
    id: 'task-detail-error',
    label: 'Task detail error',
    description: '模擬局部區塊讀取失敗，應使用 section scope error state。',
    type: 'error',
    message: '無法載入任務詳細資訊，請重新整理後再試。',
    scope: 'section',
  },
]

const activeScenarioId = ref(scenarios[0].id)

const activeScenario = computed(() =>
  scenarios.find((scenario) => scenario.id === activeScenarioId.value) ?? scenarios[0],
)
</script>