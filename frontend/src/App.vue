<script setup lang="ts">
import Button from 'primevue/button'
import Card from 'primevue/card'
import Divider from 'primevue/divider'
import Tag from 'primevue/tag'

const pillars = [
  {
    title: 'Workflow 視角',
    body: '把 backlog、任務狀態與責任分工放進同一條操作脈絡，降低切換成本。',
  },
  {
    title: 'Outside-In 開發',
    body: '先把畫面與互動定型，再逐步推進 API、規則與資料儲存層。',
  },
  {
    title: '可驗證交付',
    body: '使用 Playwright 建立 E2E 基線，讓主要流程從一開始就可驗證。',
  },
]

const milestones = [
  'Vue 3 + TypeScript + Vite 專案初始化完成',
  'Tailwind CSS 與 PrimeVue 已接入應用程式入口',
  'Playwright 設定與範例測試已建立',
]

const stack = ['Vue 3', 'TypeScript', 'Vite', 'Tailwind CSS', 'PrimeVue', 'Playwright']
</script>

<template>
  <main class="relative isolate min-h-screen overflow-hidden px-6 py-8 sm:px-8 lg:px-12">
    <div class="grid-noise"></div>
    <div class="absolute inset-x-0 top-0 -z-10 h-[28rem] bg-[radial-gradient(circle_at_top_left,_rgba(14,165,233,0.22),_transparent_34%),radial-gradient(circle_at_top_right,_rgba(249,115,22,0.16),_transparent_26%)]"></div>

    <section class="mx-auto flex min-h-[calc(100vh-4rem)] max-w-7xl flex-col">
      <header class="motion-rise flex items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <span class="brand-orbit"></span>
          <div>
            <p class="text-xs font-semibold uppercase tracking-[0.42em] text-slate-500">RonFlow</p>
            <p class="text-sm text-slate-600">Project management frontend foundation</p>
          </div>
        </div>

        <Tag value="Frontend initialized" severity="contrast" rounded />
      </header>

      <section class="grid flex-1 items-center gap-10 py-10 lg:grid-cols-[1.15fr_0.85fr] lg:py-14">
        <div class="space-y-8">
          <div class="motion-rise motion-delay-1 space-y-5">
            <p class="section-kicker">Outside-In Product Flow</p>
            <h1
              data-testid="hero-title"
              class="max-w-4xl text-5xl font-black leading-[0.95] tracking-[-0.05em] text-slate-950 sm:text-6xl"
            >
              把專案流程、任務節奏與團隊視角放進同一個畫面。
            </h1>
            <p class="max-w-2xl text-lg leading-8 text-slate-700 sm:text-xl">
              這個前端基底已經完成 Vue 3、TypeScript、Vite、Tailwind CSS、PrimeVue 與 Playwright
              的整合，可直接延伸成 dashboard、workflow board 與 task execution UI。
            </p>
          </div>

          <div class="motion-rise motion-delay-2 flex flex-wrap gap-3">
            <Button label="啟動前端基底" icon="pi pi-play" data-testid="primary-cta" />
            <Button label="查看技術棧" icon="pi pi-arrow-right" severity="secondary" outlined />
          </div>

          <ul class="motion-rise motion-delay-2 flex flex-wrap gap-3" aria-label="Technology stack">
            <li v-for="item in stack" :key="item">
              <span class="stack-chip" data-testid="stack-chip">{{ item }}</span>
            </li>
          </ul>
        </div>

        <div class="motion-rise motion-delay-2 glass-panel rounded-[2rem] p-4 sm:p-6">
          <div class="grid gap-4">
            <Card class="border border-white/60 bg-white/75 shadow-none backdrop-blur">
              <template #content>
                <div class="space-y-4">
                  <div class="flex items-start justify-between gap-4">
                    <div>
                      <p class="section-kicker">Delivery radar</p>
                      <h2 class="text-2xl font-semibold text-slate-950">Foundation status</h2>
                    </div>
                    <Tag value="Ready" severity="success" />
                  </div>

                  <div class="grid gap-3 sm:grid-cols-3">
                    <div class="stat-chip">
                      <span class="stat-label">UI Core</span>
                      <strong class="stat-value">PrimeVue</strong>
                    </div>
                    <div class="stat-chip">
                      <span class="stat-label">Styling</span>
                      <strong class="stat-value">Tailwind</strong>
                    </div>
                    <div class="stat-chip">
                      <span class="stat-label">E2E</span>
                      <strong class="stat-value">Playwright</strong>
                    </div>
                  </div>
                </div>
              </template>
            </Card>

            <Card class="border border-white/60 bg-slate-950 text-white shadow-none">
              <template #content>
                <div class="space-y-4">
                  <div class="flex items-center justify-between gap-4">
                    <div>
                      <p class="text-xs font-semibold uppercase tracking-[0.3em] text-sky-300/80">
                        Build slice
                      </p>
                      <h2 class="text-2xl font-semibold">What is ready now</h2>
                    </div>
                    <i class="pi pi-sparkles rounded-full bg-white/10 p-3 text-sky-300"></i>
                  </div>

                  <ol class="space-y-3 text-sm leading-7 text-slate-200">
                    <li
                      v-for="(item, index) in milestones"
                      :key="item"
                      class="flex items-start gap-3 rounded-2xl border border-white/10 bg-white/5 px-4 py-3"
                    >
                      <span class="mt-0.5 inline-flex h-7 w-7 items-center justify-center rounded-full bg-sky-300/15 text-sm font-semibold text-sky-200">
                        {{ index + 1 }}
                      </span>
                      <span>{{ item }}</span>
                    </li>
                  </ol>
                </div>
              </template>
            </Card>
          </div>
        </div>
      </section>

      <Divider class="my-0" />

      <section class="grid gap-4 py-8 md:grid-cols-3">
        <Card
          v-for="pillar in pillars"
          :key="pillar.title"
          class="glass-panel rounded-[1.5rem] border border-white/60 bg-white/72 shadow-none"
        >
          <template #content>
            <div class="space-y-3">
              <p class="section-kicker">Core principle</p>
              <h3 class="text-xl font-semibold text-slate-950">{{ pillar.title }}</h3>
              <p class="leading-7 text-slate-700">{{ pillar.body }}</p>
            </div>
          </template>
        </Card>
      </section>
    </section>
  </main>
</template>
