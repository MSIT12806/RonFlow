<template>
  <section class="board-panel collaboration-panel">
    <header class="board-header">
      <div>
        <p class="eyebrow">Project invitations</p>
        <h2 class="board-title">邀請收件匣</h2>
        <p class="empty-copy collaboration-intro">
          先提供最小 inbox shell，後續再接上真實邀請清單與接受 / 拒絕流程。
        </p>
      </div>

      <button type="button" class="secondary-button" @click="$emit('back-to-board')">
        返回看板
      </button>
    </header>

    <section class="collaboration-card collaboration-card-wide">
      <div class="panel-heading-row">
        <div>
          <p class="eyebrow">Inbox</p>
          <h3>待處理邀請</h3>
        </div>
        <span class="count-badge">0</span>
      </div>

      <p class="empty-copy collaboration-empty-copy">
        Invitation API 尚未接上，這裡先保留邀請清單與主要操作區域。
      </p>

      <div class="board-header-actions collaboration-actions">
        <button type="button" class="primary-button">接受邀請</button>
        <button type="button" class="secondary-button">拒絕邀請</button>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
defineEmits<{
  (event: 'back-to-board'): void
}>()
</script>