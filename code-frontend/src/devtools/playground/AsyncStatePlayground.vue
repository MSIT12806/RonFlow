<template>
  <main class="playground-shell">
    <section class="playground-card">
      <header class="playground-header">
        <div>
          <p class="eyebrow">CommonSpec playground</p>
          <h1>API resource views</h1>
          <p class="playground-copy">
            使用這個 dev-only playground 可以穩定人工檢查 query / command resource view 的畫面，不需要等待真實 API 剛好失敗。
          </p>
        </div>

        <a class="secondary-button playground-link" href="/">回到 RonFlow</a>
      </header>

      <section class="playground-examples">
        <article class="playground-example-card">
          <div>
            <p class="detail-label">Query view</p>
            <h2>ApiQueryResourceView</h2>
            <p class="playground-copy">展示 query resource 在 loading、error、success 三種狀態下的 shared 畫面。</p>
          </div>

          <div class="playground-toolbar" role="tablist" aria-label="切換 query resource view 狀態">
            <button
              v-for="state in queryStates"
              :key="state.id"
              type="button"
              class="playground-toggle"
              :class="{ 'playground-toggle-active': state.id === activeQueryStateId }"
              :aria-pressed="state.id === activeQueryStateId"
              @click="activeQueryStateId = state.id"
            >
              {{ state.label }}
            </button>
          </div>

          <ApiQueryResourceView
            :is-loading="activeQueryState.type === 'loading'"
            :error-message="activeQueryState.type === 'error' ? activeQueryState.message : ''"
            :loading-message="activeQueryState.loadingMessage"
          >
            <div v-if="activeQueryState.type === 'success'" class="playground-query-result">
              <p class="detail-label">Loaded data</p>
              <h3>測試資料已載入</h3>
              <p class="playground-copy">這裡代表 query resource 成功後，畫面回到實際內容區塊。</p>
            </div>
          </ApiQueryResourceView>
        </article>

        <article class="playground-example-card">
          <div>
            <p class="detail-label">Command view</p>
            <h2>ApiCommandResourceView</h2>
            <p class="playground-copy">展示 command form 在 idle、submitting、error 狀態下如何保留表單本體並顯示 shared feedback。</p>
          </div>

          <div class="playground-toolbar" role="tablist" aria-label="切換 command resource view 狀態">
            <button
              v-for="state in commandStates"
              :key="state.id"
              type="button"
              class="playground-toggle"
              :class="{ 'playground-toggle-active': state.id === activeCommandStateId }"
              :aria-pressed="state.id === activeCommandStateId"
              @click="activeCommandStateId = state.id"
            >
              {{ state.label }}
            </button>
          </div>

          <ApiCommandResourceView
            :is-submitting="activeCommandState.type === 'submitting'"
            :error-message="activeCommandState.type === 'error' ? activeCommandState.message : ''"
            submitting-message="正在送出測試命令，請稍候..."
          >
            <form class="playground-command-form" @submit.prevent>
              <label for="playground-command-title">測試欄位</label>
              <input
                id="playground-command-title"
                type="text"
                :value="activeCommandState.fieldValue"
                :disabled="activeCommandState.disableInteraction"
              />

              <div class="modal-actions">
                <button type="button" class="secondary-button" :disabled="activeCommandState.disableInteraction">
                  取消
                </button>
                <button type="submit" class="primary-button" :disabled="activeCommandState.disableInteraction">
                  送出
                </button>
              </div>
            </form>
          </ApiCommandResourceView>
        </article>

        <article class="playground-example-card">
          <div>
            <p class="detail-label">Board command error</p>
            <h2>ProjectBoard</h2>
            <p class="playground-copy">展示 board mutation 失敗時保留看板內容，並只在區塊內顯示錯誤訊息，不切成整頁錯誤。</p>
          </div>

          <div class="playground-toolbar" role="tablist" aria-label="切換 project board 狀態">
            <button
              v-for="state in boardStates"
              :key="state.id"
              type="button"
              class="playground-toggle"
              :class="{ 'playground-toggle-active': state.id === activeBoardStateId }"
              :aria-pressed="state.id === activeBoardStateId"
              @click="activeBoardStateId = state.id"
            >
              {{ state.label }}
            </button>
          </div>

          <ProjectBoard
            active-project-name="Playground Project"
            :columns="playgroundBoardColumns"
            :is-loading-board="activeBoardState.type === 'loading'"
            :command-error-message="activeBoardState.type === 'error' ? activeBoardState.message : ''"
            @open-create-task="noop"
            @open-task-detail="noop"
            @move-task-to-state="noop"
          />
        </article>
      </section>
    </section>
  </main>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { BoardColumnResponse } from '../../api/types'
import ProjectBoard from '../../components/ProjectBoard.vue'
import ApiCommandResourceView from '../../components/bases/ApiCommandResourceView.vue'
import ApiQueryResourceView from '../../components/bases/ApiQueryResourceView.vue'

type QueryState = {
  id: string
  label: string
  type: 'loading' | 'error' | 'success'
  message: string
  loadingMessage: string
}

type CommandState = {
  id: string
  label: string
  type: 'idle' | 'submitting' | 'error'
  message: string
  fieldValue: string
  disableInteraction: boolean
}

type BoardState = {
  id: string
  label: string
  type: 'idle' | 'loading' | 'error'
  message: string
}

const queryStates: QueryState[] = [
  {
    id: 'loading',
    label: 'Loading',
    type: 'loading',
    message: '',
    loadingMessage: '正在載入測試資料...',
  },
  {
    id: 'error',
    label: 'Error',
    type: 'error',
    message: '無法載入測試資料，請稍後再試。',
    loadingMessage: '正在載入測試資料...',
  },
  {
    id: 'success',
    label: 'Success',
    type: 'success',
    message: '',
    loadingMessage: '正在載入測試資料...',
  },
]

const commandStates: CommandState[] = [
  {
    id: 'idle',
    label: 'Idle',
    type: 'idle',
    message: '',
    fieldValue: '尚未送出的測試內容',
    disableInteraction: false,
  },
  {
    id: 'submitting',
    label: 'Submitting',
    type: 'submitting',
    message: '',
    fieldValue: '送出中的測試內容',
    disableInteraction: true,
  },
  {
    id: 'error',
    label: 'Error',
    type: 'error',
    message: '測試命令送出失敗，請稍後再試。',
    fieldValue: '保留於錯誤狀態的測試內容',
    disableInteraction: false,
  },
]

const boardStates: BoardState[] = [
  {
    id: 'idle',
    label: 'Idle',
    type: 'idle',
    message: '',
  },
  {
    id: 'error',
    label: 'Command Error',
    type: 'error',
    message: '變更任務狀態失敗，請稍後再試。',
  },
  {
    id: 'loading',
    label: 'Loading',
    type: 'loading',
    message: '',
  },
]

const playgroundBoardColumns: BoardColumnResponse[] = [
  {
    stateKey: 'todo',
    label: '待處理',
    isInitialState: true,
    emptyStateMessage: '目前沒有任務',
    tasks: [
      { id: 'task-1', title: '整理 Sprint 2 測試清單' },
      { id: 'task-2', title: '確認拖曳失敗的回饋文案' },
    ],
  },
  {
    stateKey: 'active',
    label: '進行中',
    isInitialState: false,
    emptyStateMessage: '目前沒有任務',
    tasks: [{ id: 'task-3', title: '補上 board command error alert' }],
  },
  {
    stateKey: 'review',
    label: '審查中',
    isInitialState: false,
    emptyStateMessage: '目前沒有任務',
    tasks: [],
  },
  {
    stateKey: 'done',
    label: '已完成',
    isInitialState: false,
    emptyStateMessage: '目前沒有任務',
    tasks: [{ id: 'task-4', title: '完成 v0.1 scope 對齊' }],
  },
]

const activeQueryStateId = ref(queryStates[0].id)
const activeCommandStateId = ref(commandStates[0].id)
const activeBoardStateId = ref(boardStates[0].id)

const activeQueryState = computed(() =>
  queryStates.find((state) => state.id === activeQueryStateId.value) ?? queryStates[0],
)

const activeCommandState = computed(() =>
  commandStates.find((state) => state.id === activeCommandStateId.value) ?? commandStates[0],
)

const activeBoardState = computed(() =>
  boardStates.find((state) => state.id === activeBoardStateId.value) ?? boardStates[0],
)

function noop() {}
</script>
