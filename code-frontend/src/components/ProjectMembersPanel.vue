<template>
  <section class="board-panel collaboration-panel">
    <header class="board-header">
      <div>
        <p class="eyebrow">Project collaboration</p>
        <h2 class="board-title">專案成員</h2>
        <p class="empty-copy collaboration-intro">
          查看目前成員、邀請其他已註冊使用者，並追蹤待處理邀請。
        </p>
      </div>

      <button type="button" class="secondary-button" @click="$emit('back-to-board')">
        返回看板
      </button>
    </header>

    <BaseErrorState
      v-if="membersLoadErrorMessage"
      class="collaboration-load-error"
      :message="membersLoadErrorMessage"
      scope="section"
    />

    <div v-if="!membersLoadErrorMessage" class="collaboration-grid">
      <section class="collaboration-card" data-testid="project-members-list">
        <div class="panel-heading-row">
          <div>
            <p class="eyebrow">Current members</p>
            <h3>目前成員</h3>
          </div>
          <span class="count-badge">{{ members.length }}</span>
        </div>

        <ul class="collaboration-list">
          <li
            v-for="member in members"
            :key="`${member.userName}-${member.role}`"
            class="collaboration-list-item"
          >
            <div>
              <strong>{{ member.userName }}</strong>
              <small>{{ member.role === '專案擁有者' ? ownerDescription : '已接受邀請的專案成員' }}</small>
            </div>
            <span class="collaboration-role-badge">{{ member.role }}</span>
          </li>
        </ul>
      </section>

      <section class="collaboration-card" data-testid="project-online-users">
        <div class="panel-heading-row">
          <div>
            <p class="eyebrow">Online users</p>
            <h3>在線成員</h3>
          </div>
          <span class="count-badge">{{ onlineUsers.length }}</span>
        </div>

        <ul v-if="onlineUsers.length > 0" class="collaboration-list">
          <li
            v-for="onlineUser in onlineUsers"
            :key="onlineUser.userName"
            class="collaboration-list-item"
          >
            <div>
              <strong>{{ onlineUser.userName }}</strong>
              <small>目前仍在這個 Project scope 中</small>
            </div>
          </li>
        </ul>

        <p v-else class="empty-copy collaboration-empty-copy">目前沒有在線成員</p>
      </section>

      <section class="collaboration-card">
        <div class="panel-heading-row">
          <div>
            <p class="eyebrow">Invite</p>
            <h3>邀請成員</h3>
          </div>
        </div>

        <form class="modal-form collaboration-form" @submit.prevent="submitInvitation">
          <label for="invite-target">邀請對象</label>
          <input
            id="invite-target"
            v-model="invitee"
            type="text"
            placeholder="輸入帳號或電子郵件"
            :disabled="isSubmitting"
          />
          <p v-if="inviteeError" class="error-copy">{{ inviteeError }}</p>

          <ApiCommandResourceView
            :is-submitting="isSubmitting"
            :error-message="commandErrorMessage"
            submitting-message="正在送出邀請，請稍候..."
          />

          <div class="board-header-actions">
            <button type="submit" class="primary-button" :disabled="isSubmitting">邀請</button>
          </div>
        </form>
      </section>

      <section class="collaboration-card collaboration-card-wide">
        <div class="panel-heading-row">
          <div>
            <p class="eyebrow">Pending invitations</p>
            <h3>待處理邀請</h3>
          </div>
          <span class="count-badge">{{ pendingInvitations.length }}</span>
        </div>

        <BaseErrorState
          v-if="invitationsLoadErrorMessage"
          :message="invitationsLoadErrorMessage"
          scope="section"
        />

        <ul v-else-if="pendingInvitations.length > 0" class="collaboration-list">
          <li
            v-for="invitation in pendingInvitations"
            :key="invitation.id"
            class="collaboration-list-item"
          >
            <div>
              <strong>{{ invitation.invitee }}</strong>
              <small>待接受邀請</small>
            </div>
          </li>
        </ul>

        <p v-else class="empty-copy collaboration-empty-copy">目前沒有待處理邀請</p>
      </section>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { ProjectCommandService, ProjectQueryService } from '../application'
import {
  ApiRequestError,
  ApiValidationError,
  type ProjectInvitationResponse,
  type ProjectMemberResponse,
  type ProjectOnlineUserResponse,
} from '../api/ronflowApi'
import BaseErrorState from './bases/BaseErrorState.vue'
import ApiCommandResourceView from './bases/ApiCommandResourceView.vue'
import { useApiResource } from '../composables/useApiResource'

const props = defineProps<{
  activeProjectId: string | null
  activeProjectName: string | null
  currentUserName: string | null
}>()

defineEmits<{
  (event: 'back-to-board'): void
}>()

const projectQueryService = new ProjectQueryService()
const projectCommandService = new ProjectCommandService()

const membersResource = useApiResource(
  (projectId: string) => projectQueryService.getMembers(projectId),
  { createInitialData: () => ({ items: [] as ProjectMemberResponse[], onlineUsers: [] as ProjectOnlineUserResponse[] }) },
)

const invitationsResource = useApiResource(
  (projectId: string) => projectQueryService.getPendingInvitations(projectId),
  { createInitialData: () => ({ items: [] as ProjectInvitationResponse[] }) },
)

const inviteMemberResource = useApiResource(
  (projectId: string, invitee: string) => projectCommandService.inviteMember(projectId, invitee),
  {
    mapErrorMessage: (error) => error instanceof ApiValidationError ? '' : '邀請成員失敗，請稍後再試。',
  },
)

const invitee = ref('')
const inviteeError = ref('')
const membersLoadErrorMessage = ref('')
const invitationsLoadErrorMessage = ref('')
let collaborationPollTimer: ReturnType<typeof window.setInterval> | null = null

const members = computed(() => {
  const items = membersResource.data.value?.items ?? []

  if (items.length > 0) {
    return items
  }

  return [{
    userName: props.currentUserName || '目前使用者',
    role: '專案擁有者',
  }]
})

const pendingInvitations = computed(() => invitationsResource.data.value?.items ?? [])
const onlineUsers = computed(() => membersResource.data.value?.onlineUsers ?? [])
const ownerDescription = computed(() => props.activeProjectName ? `${props.activeProjectName} 的建立者` : '目前建立此專案的使用者')
const isSubmitting = computed(() => inviteMemberResource.isLoading.value)
const commandErrorMessage = computed(() => inviteMemberResource.errorMessage.value)

watch(() => props.activeProjectId, async (projectId) => {
  if (!projectId) {
    stopCollaborationPolling()
    membersResource.reset()
    invitationsResource.reset()
    return
  }

  await loadCollaborationData(projectId)
  startCollaborationPolling(projectId)
}, { immediate: true })

onUnmounted(() => {
  stopCollaborationPolling()
})

async function loadCollaborationData(projectId: string) {
  membersLoadErrorMessage.value = ''
  invitationsLoadErrorMessage.value = ''

  const [membersResult, invitationsResult] = await Promise.allSettled([
    membersResource.execute(projectId),
    invitationsResource.execute(projectId),
  ])

  const membersFailed = membersResult.status === 'rejected'
    && !(membersResult.reason instanceof ApiRequestError && membersResult.reason.status === 404)
  const invitationsFailed = invitationsResult.status === 'rejected'
    && !(invitationsResult.reason instanceof ApiRequestError && invitationsResult.reason.status === 404)

  if (membersFailed) {
    membersLoadErrorMessage.value = '無法載入專案成員資料，請稍後再試。'
  }

  if (!membersFailed && invitationsFailed) {
    invitationsLoadErrorMessage.value = '無法載入待處理邀請，請稍後再試。'
  }
}

function startCollaborationPolling(projectId: string) {
  stopCollaborationPolling()
  collaborationPollTimer = window.setInterval(() => {
    void loadCollaborationData(projectId)
  }, 3000)
}

function stopCollaborationPolling() {
  if (collaborationPollTimer !== null) {
    window.clearInterval(collaborationPollTimer)
    collaborationPollTimer = null
  }
}

async function submitInvitation() {
  if (!props.activeProjectId) {
    return
  }

  inviteeError.value = ''
  inviteMemberResource.reset()

  const inviteeValue = invitee.value.trim()
  if (!inviteeValue) {
    inviteeError.value = '邀請對象為必填欄位'
    return
  }

  try {
    const invitation = await inviteMemberResource.execute(props.activeProjectId, inviteeValue)
    invitationsResource.setData({
      items: [...pendingInvitations.value, invitation],
    })
    invitee.value = ''
  } catch (error) {
    if (error instanceof ApiValidationError) {
      inviteeError.value = error.errors.invitee?.[0] ?? error.errors.target?.[0] ?? '邀請對象為必填欄位'
    }
  }
}
</script>