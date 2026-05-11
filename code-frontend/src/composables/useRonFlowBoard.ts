import { computed, onMounted, ref } from 'vue'
import {
  ApiRequestError,
  type ProjectBoardResponse,
  type ProjectListItemResponse,
  type TaskDetailResponse,
  type WorkflowKey,
} from '../api/ronflowApi'
import {
  ProjectCommandService,
  ProjectQueryService,
  TaskQueryService,
  TaskCommandService,
} from '../application'
import { useApiResource } from './useApiResource'

const workflowColumns: Array<{ key: WorkflowKey; label: string }> = [
  { key: 'todo', label: '待處理' },
  { key: 'active', label: '進行中' },
  { key: 'review', label: '審查中' },
  { key: 'done', label: '已完成' },
]

const projectQueryService = new ProjectQueryService()
const projectCommandService = new ProjectCommandService()
const taskQueryService = new TaskQueryService()
const taskCommandService = new TaskCommandService()

export function useRonFlowBoard() {
  const activeProjectId = ref<string | null>(null)
  const isTaskDetailOpen = ref(false)

  const pageError = ref('')

  const projectsResource = useApiResource(
    () => projectQueryService.getProjects(),
    { createInitialData: () => ({ items: [] as ProjectListItemResponse[] }) },
  )

  const boardResource = useApiResource(
    (projectId: string) => projectQueryService.getBoard(projectId),
    { clearDataOnExecute: true },
  )

  const taskDetailResource = useApiResource(
    (projectId: string, taskId: string) => taskQueryService.getDetail(projectId, taskId),
    {
      clearDataOnExecute: true,
      mapErrorMessage: () => '無法載入任務詳細資訊，請重新整理後再試。',
    },
  )

  const changeTaskStateResource = useApiResource(
    (projectId: string, taskId: string, stateKey: WorkflowKey) =>
      taskCommandService.changeState(projectId, taskId, stateKey),
  )

  const projects = computed(() => projectsResource.data.value?.items ?? [])
  const activeBoard = computed(() => boardResource.data.value)
  const selectedTask = computed(() => taskDetailResource.data.value)
  const taskDetailError = computed(() => taskDetailResource.errorMessage.value)
  const isLoadingProjects = computed(() => projectsResource.isLoading.value)
  const isLoadingBoard = computed(() => boardResource.isLoading.value)
  const isLoadingTaskDetail = computed(() => taskDetailResource.isLoading.value)

  const activeProject = computed(() =>
    projects.value.find((project) => project.id === activeProjectId.value) ?? null,
  )

  const activeColumns = computed(() => activeBoard.value?.columns ?? workflowColumns.map((column) => ({
    stateKey: column.key,
    label: column.label,
    isInitialState: column.key === 'todo',
    emptyStateMessage: '目前沒有任務',
    tasks: [],
  })))

  onMounted(async () => {
    await loadProjects()
  })



  async function openTaskDetail(taskId: string) {
    if (!activeProjectId.value) {
      return
    }

    taskDetailResource.reset()
    isTaskDetailOpen.value = true

    try {
      await taskDetailResource.execute(activeProjectId.value, taskId)
    } catch {}
  }

  async function selectProject(projectId: string) {
    activeProjectId.value = projectId
    taskDetailResource.reset()
    isTaskDetailOpen.value = false
    await loadBoard(projectId)
  }

  function closeTaskDetail() {
    isTaskDetailOpen.value = false
    taskDetailResource.reset()
  }

  async function moveTaskToState(taskId: string, stateKey: WorkflowKey) {
    if (!activeProjectId.value) {
      return
    }

    pageError.value = ''

    try {
      const updatedTask = await changeTaskStateResource.execute(activeProjectId.value, taskId, stateKey)

      if (selectedTask.value?.id === taskId) {
        taskDetailResource.setData(updatedTask)
      }

      await loadBoard(activeProjectId.value)
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 404) {
        pageError.value = '找不到指定的任務，請重新整理專案看板。'
        return
      }

      pageError.value = '變更任務狀態失敗，請稍後再試。'
    }
  }

  function getTasksByStatus(status: WorkflowKey) {
    return activeColumns.value.find((column) => column.stateKey === status)?.tasks ?? []
  }

  function formatProjectMeta(updatedAt: string) {
    return `更新於 ${new Intl.DateTimeFormat('zh-TW', {
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(updatedAt))}`
  }

  function formatTimelineTime(occurredAt: string) {
    return new Intl.DateTimeFormat('zh-TW', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(occurredAt))
  }

  async function loadProjects(preferredProjectId?: string) {
    pageError.value = ''

    try {
      const projectList = await projectsResource.execute()

      if (projectList.items.length === 0) {
        activeProjectId.value = null
        boardResource.reset()
        taskDetailResource.reset()
        isTaskDetailOpen.value = false
        return
      }

      const nextProjectId = preferredProjectId && projectList.items.some((project) => project.id === preferredProjectId)
        ? preferredProjectId
        : activeProjectId.value && projectList.items.some((project) => project.id === activeProjectId.value)
          ? activeProjectId.value
          : projectList.items[0].id

      activeProjectId.value = nextProjectId
      await loadBoard(nextProjectId)
    } catch {
      pageError.value = '無法載入專案列表，請確認後端 API 已啟動。'
    }
  }

  async function loadBoard(projectId: string) {
    pageError.value = ''

    try {
      await boardResource.execute(projectId)
    } catch (error) {
      boardResource.reset()

      if (error instanceof ApiRequestError && error.status === 404) {
        pageError.value = '找不到指定的專案看板，請重新整理列表。'
        await loadProjects()
        return
      }

      pageError.value = '無法載入專案看板，請稍後再試。'
    }
  }



  return {
    projects,
    activeProjectId,
    activeProject,
    activeColumns,
    selectedTask,
    isTaskDetailOpen,
    taskDetailError,
    workflowColumns,
    isLoadingProjects,
    isLoadingBoard,
    isLoadingTaskDetail,
    pageError,
    openTaskDetail,
    selectProject,
    closeTaskDetail,
    moveTaskToState,
    loadProjects,
    loadBoard,
    getTasksByStatus,
    formatProjectMeta,
    formatTimelineTime,
  }
}