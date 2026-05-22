<template>
  <section class="board-panel collaboration-panel">
    <header class="board-header">
      <div>
        <p class="eyebrow">Project collaboration</p>
        <h2 class="board-title">專案成員</h2>
        <p class="empty-copy collaboration-intro">
          先提供最小 collaboration shell，後續再接上真實的 invitation 與 member API。
        </p>
      </div>

      <button type="button" class="secondary-button" @click="$emit('back-to-board')">
        返回看板
      </button>
    </header>

    <div class="collaboration-grid">
      <section class="collaboration-card">
        <div class="panel-heading-row">
          <div>
            <p class="eyebrow">Current members</p>
            <h3>目前成員</h3>
          </div>
          <span class="count-badge">1</span>
        </div>

        <ul class="collaboration-list">
          <li class="collaboration-list-item">
            <div>
              <strong>{{ currentUserName || '目前使用者' }}</strong>
              <small>{{ activeProjectName ? `${activeProjectName} 的建立者` : '目前建立此專案的使用者' }}</small>
            </div>
            <span class="collaboration-role-badge">專案擁有者</span>
          </li>
        </ul>
      </section>

      <section class="collaboration-card">
        <div class="panel-heading-row">
          <div>
            <p class="eyebrow">Invite</p>
            <h3>邀請成員</h3>
          </div>
        </div>

        <form class="modal-form collaboration-form" @submit.prevent>
          <label for="invite-target">邀請對象</label>
          <input id="invite-target" type="text" placeholder="輸入帳號或電子郵件" />

          <div class="board-header-actions">
            <button type="submit" class="primary-button">邀請</button>
          </div>
        </form>
      </section>

      <section class="collaboration-card collaboration-card-wide">
        <div class="panel-heading-row">
          <div>
            <p class="eyebrow">Pending invitations</p>
            <h3>待處理邀請</h3>
          </div>
          <span class="count-badge">0</span>
        </div>

        <p class="empty-copy collaboration-empty-copy">
          目前先保留待處理邀請的畫面位置，之後會接上 Project Invitation 查詢與送出結果。
        </p>
      </section>
    </div>
  </section>
</template>

<script setup lang="ts">
defineProps<{
  activeProjectName: string | null
  currentUserName: string | null
}>()

defineEmits<{
  (event: 'back-to-board'): void
}>()
</script>