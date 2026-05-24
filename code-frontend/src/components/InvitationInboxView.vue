<template>
  <section class="board-panel collaboration-panel">
    <header class="board-header">
      <div>
        <p class="eyebrow">Project invitations</p>
        <h2 class="board-title">邀請收件匣</h2>
        <p class="empty-copy collaboration-intro">
          查看寄送給目前使用者的待處理邀請，並決定接受或拒絕。
        </p>
      </div>

      <button type="button" class="secondary-button" @click="$emit('back-to-board')">
        返回看板
      </button>
    </header>

    <BaseErrorState
      v-if="loadErrorMessage"
      class="collaboration-load-error"
      :message="loadErrorMessage"
      scope="section"
    />

    <section class="collaboration-card collaboration-card-wide">
      <div class="panel-heading-row">
        <div>
          <p class="eyebrow">Inbox</p>
          <h3>待處理邀請</h3>
        </div>
        <span class="count-badge">{{ invitations.length }}</span>
      </div>

      <ul v-if="!loadErrorMessage && invitations.length > 0" class="collaboration-list">
        <li
          v-for="invitation in invitations"
          :key="invitation.id"
          class="collaboration-list-item"
          data-testid="invitation-item"
        >
          <div>
            <strong>{{ invitation.projectName || invitation.invitee }}</strong>
            <small>{{ invitation.inviterName ? `邀請者：${invitation.inviterName}` : '待處理邀請' }}</small>
          </div>

          <div class="board-header-actions collaboration-actions">
            <button
              type="button"
              class="primary-button"
              :disabled="isProcessingInvitation"
              @click="onAcceptInvitation(invitation.id)"
            >
              接受邀請
            </button>
            <button
              type="button"
              class="secondary-button"
              :disabled="isProcessingInvitation"
              @click="onRejectInvitation(invitation.id)"
            >
              拒絕邀請
            </button>
          </div>
        </li>
      </ul>

      <p v-else-if="!loadErrorMessage" class="empty-copy collaboration-empty-copy">目前沒有待處理邀請</p>

      <ApiCommandResourceView
        :is-submitting="isProcessingInvitation"
        :error-message="commandErrorMessage"
        submitting-message="正在處理邀請，請稍候..."
      />

      <p
        v-if="syncMessage"
        class="command-resource-feedback"
        data-testid="invitation-sync-message"
        role="status"
      >
        {{ syncMessage }}
      </p>

      <div v-if="!loadErrorMessage && invitations.length === 0" class="board-header-actions collaboration-actions">
        <button type="button" class="primary-button" disabled>接受邀請</button>
        <button type="button" class="secondary-button" disabled>拒絕邀請</button>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { ProjectCommandService, ProjectQueryService } from '../application'
import { ApiRequestError, type ProjectInvitationResponse } from '../api/ronflowApi'
import ApiCommandResourceView from './bases/ApiCommandResourceView.vue'
import BaseErrorState from './bases/BaseErrorState.vue'
import { useApiResource } from '../composables/useApiResource'

const emit = defineEmits<{
  (event: 'back-to-board'): void
  (event: 'invitation-accepted'): void
  (event: 'invitations-changed'): void
}>()

const projectQueryService = new ProjectQueryService()
const projectCommandService = new ProjectCommandService()

const invitationsResource = useApiResource(
  () => projectQueryService.getInvitationInbox(),
  { createInitialData: () => ({ items: [] as ProjectInvitationResponse[] }) },
)

const acceptInvitationResource = useApiResource(
  (invitationId: string) => projectCommandService.acceptInvitation(invitationId),
  { mapErrorMessage: () => '接受邀請失敗，請稍後再試。' },
)

const rejectInvitationResource = useApiResource(
  (invitationId: string) => projectCommandService.rejectInvitation(invitationId),
  { mapErrorMessage: () => '拒絕邀請失敗，請稍後再試。' },
)

const loadErrorMessage = ref('')
const syncMessage = ref('')
let invitationInboxPollTimer: ReturnType<typeof window.setInterval> | null = null
let invitationInboxPollStartTimer: ReturnType<typeof window.setTimeout> | null = null

const invitations = computed(() => invitationsResource.data.value?.items ?? [])
const isProcessingInvitation = computed(() => acceptInvitationResource.isLoading.value || rejectInvitationResource.isLoading.value)
const commandErrorMessage = computed(() => acceptInvitationResource.errorMessage.value || rejectInvitationResource.errorMessage.value)

onMounted(async () => {
  await reloadInvitationInbox()
  startInvitationInboxPolling()
})

onUnmounted(() => {
  stopInvitationInboxPolling()
})

async function reloadInvitationInbox() {
  loadErrorMessage.value = ''

  try {
    await invitationsResource.execute()
  } catch (error) {
    if (!(error instanceof ApiRequestError && error.status === 404)) {
      loadErrorMessage.value = '無法載入邀請收件匣，請稍後再試。'
    }
  }
}

async function pollInvitationInbox() {
  if (isProcessingInvitation.value) {
    return
  }

  const previousIds = invitations.value.map((invitation) => invitation.id).join('|')
  await reloadInvitationInbox()

  if (!loadErrorMessage.value) {
    const nextIds = invitations.value.map((invitation) => invitation.id).join('|')
    if (previousIds !== nextIds) {
      emit('invitations-changed')
    }
  }
}

function startInvitationInboxPolling() {
  stopInvitationInboxPolling()
  invitationInboxPollStartTimer = window.setTimeout(() => {
    void pollInvitationInbox()
    invitationInboxPollTimer = window.setInterval(() => {
      void pollInvitationInbox()
    }, 5000)
  }, 6000)
}

function stopInvitationInboxPolling() {
  if (invitationInboxPollStartTimer !== null) {
    window.clearTimeout(invitationInboxPollStartTimer)
    invitationInboxPollStartTimer = null
  }

  if (invitationInboxPollTimer !== null) {
    window.clearInterval(invitationInboxPollTimer)
    invitationInboxPollTimer = null
  }
}

function isInvitationConflict(error: unknown) {
  return error instanceof ApiRequestError && (error.status === 404 || error.status === 409)
}

async function syncInvitationConflict() {
  acceptInvitationResource.reset()
  rejectInvitationResource.reset()
  await reloadInvitationInbox()

  if (loadErrorMessage.value) {
    return
  }

  syncMessage.value = '此邀請狀態已變更，已更新收件匣。'
  emit('invitations-changed')
}

async function onAcceptInvitation(invitationId: string) {
  acceptInvitationResource.reset()
  rejectInvitationResource.reset()
  syncMessage.value = ''

  try {
    await acceptInvitationResource.execute(invitationId)
    invitationsResource.setData({
      items: invitations.value.filter((invitation) => invitation.id !== invitationId),
    })
    emit('invitation-accepted')
    emit('invitations-changed')
  } catch (error) {
    if (isInvitationConflict(error)) {
      await syncInvitationConflict()
    }
  }
}

async function onRejectInvitation(invitationId: string) {
  acceptInvitationResource.reset()
  rejectInvitationResource.reset()
  syncMessage.value = ''

  try {
    await rejectInvitationResource.execute(invitationId)
    invitationsResource.setData({
      items: invitations.value.filter((invitation) => invitation.id !== invitationId),
    })
    emit('invitations-changed')
  } catch (error) {
    if (isInvitationConflict(error)) {
      await syncInvitationConflict()
    }
  }
}
</script>