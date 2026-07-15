import { computed, ref } from 'vue'
import {
  HubConnectionBuilder,
  HubConnectionState,
  type HubConnection,
} from '@microsoft/signalr'
import { getDatabaseSyncOperations, type DatabaseSyncOperationResponse } from '../api/ronflowApi'
import { apiBaseUrl } from '../api/request'
import { ronAuthAccessTokenStore } from '../auth/ronauthClient'
import { getRonFlowSessionId } from '../ronflowSession'

const seenStorageKey = 'ronflow.databaseSyncNotifications.seenOperationIds'

export function useDatabaseSyncNotifications() {
  const operations = ref<DatabaseSyncOperationResponse[]>([])
  const seenOperationIds = ref<Set<string>>(loadSeenOperationIds())
  const isConnected = ref(false)
  const errorMessage = ref('')
  let connection: HubConnection | null = null
  let startPromise: Promise<void> | null = null

  const unreadCount = computed(() =>
    operations.value.filter((operation) => isCompleted(operation) && !seenOperationIds.value.has(operation.id)).length,
  )

  async function start() {
    if (typeof window === 'undefined') {
      return
    }

    if (startPromise) {
      await startPromise
      return
    }

    startPromise = startInternal()
      .catch((error) => {
        errorMessage.value = 'Git 同步通知連線失敗，會於下一次登入或重整後再嘗試。'
        console.warn(error)
      })
      .finally(() => {
        startPromise = null
      })

    await startPromise
  }

  async function stop() {
    startPromise = null
    isConnected.value = false
    errorMessage.value = ''

    if (!connection) {
      return
    }

    const currentConnection = connection
    connection = null
    currentConnection.off('databaseSyncCompleted')
    await currentConnection.stop()
  }

  function markAllSeen() {
    const nextSeenOperationIds = new Set(seenOperationIds.value)
    for (const operation of operations.value) {
      nextSeenOperationIds.add(operation.id)
    }

    seenOperationIds.value = nextSeenOperationIds
    saveSeenOperationIds(nextSeenOperationIds)
  }

  async function refreshRecent(markFetchedAsSeen = false) {
    try {
      const response = await getDatabaseSyncOperations()
      mergeOperations(response.items)

      if (markFetchedAsSeen) {
        markAllSeen()
      }
    } catch {
      errorMessage.value = '無法載入 Git 同步通知。'
    }
  }

  async function startInternal() {
    await refreshRecent(true)

    if (!connection) {
      connection = new HubConnectionBuilder()
        .withUrl(createHubUrl(), {
          accessTokenFactory: () => ronAuthAccessTokenStore.get() ?? '',
        })
        .withAutomaticReconnect()
        .build()

      connection.on('databaseSyncCompleted', (operation: DatabaseSyncOperationResponse) => {
        mergeOperations([operation])
      })

      connection.onreconnected(() => {
        isConnected.value = true
        void registerCurrentSession()
        void refreshRecent()
      })

      connection.onclose(() => {
        isConnected.value = false
      })
    }

    if (connection.state === HubConnectionState.Disconnected) {
      await connection.start()
    }

    await registerCurrentSession()
    isConnected.value = true
    errorMessage.value = ''
  }

  async function registerCurrentSession() {
    if (!connection || connection.state !== HubConnectionState.Connected) {
      return
    }

    await connection.invoke('RegisterSession', getRonFlowSessionId())
  }

  function mergeOperations(incomingOperations: DatabaseSyncOperationResponse[]) {
    const byId = new Map(operations.value.map((operation) => [operation.id, operation]))
    for (const operation of incomingOperations) {
      byId.set(operation.id, operation)
    }

    operations.value = Array.from(byId.values())
      .sort((left, right) => right.requestedAt.localeCompare(left.requestedAt))
      .slice(0, 20)
  }

  return {
    operations,
    unreadCount,
    isConnected,
    errorMessage,
    start,
    stop,
    markAllSeen,
    refreshRecent,
  }
}

function createHubUrl() {
  const trimmedApiBaseUrl = apiBaseUrl.replace(/\/+$/, '')
  const apiRoot = trimmedApiBaseUrl.endsWith('/api')
    ? trimmedApiBaseUrl.slice(0, -4)
    : trimmedApiBaseUrl

  return `${apiRoot}/hubs/database-sync-notifications`
}

function isCompleted(operation: DatabaseSyncOperationResponse) {
  return operation.status === 'succeeded' || operation.status === 'failed'
}

function loadSeenOperationIds() {
  if (typeof window === 'undefined') {
    return new Set<string>()
  }

  try {
    return new Set(JSON.parse(window.localStorage.getItem(seenStorageKey) ?? '[]') as string[])
  } catch {
    return new Set<string>()
  }
}

function saveSeenOperationIds(operationIds: Set<string>) {
  if (typeof window === 'undefined') {
    return
  }

  window.localStorage.setItem(seenStorageKey, JSON.stringify(Array.from(operationIds).slice(-100)))
}
