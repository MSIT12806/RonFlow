import { computed, onMounted, ref } from 'vue'
import {
  ApiRequestError,
  type ProjectBoardResponse,
  type ProjectListItemResponse,
  type TaskDetailResponse,
  type WorkflowKey,
} from '../api/ronflowApi'
import {
  ProjectQueryService,
  TaskQueryService,
  TaskCommandService,
} from '../application'

const workflowColumns: Array<{ key: WorkflowKey; label: string }> = [
  { key: 'todo', label: '待處理' },
  { key: 'active', label: '進行中' },
  { key: 'review', label: '審查中' },
  { key: 'done', label: '已完成' },
]

const projectQueryService = new ProjectQueryService()
const taskQueryService = new TaskQueryService()

export function useRonFlowBoard() {
  const projects = ref<ProjectListItemResponse[]>([])
  const activeProjectId = ref<string | null>(null)
  const activeBoard = ref<ProjectBoardResponse | null>(null)
  const selectedTask = ref<TaskDetailResponse | null>(null)
  const isTaskDetailOpen = ref(false)
  const taskDetailError = ref('')

  const isLoadingProjects = ref(false)
  const isLoadingBoard = ref(false)
  const isLoadingTaskDetail = ref(false)

  const pageError = ref('')

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

    selectedTask.value = null
    taskDetailError.value = ''
    isTaskDetailOpen.value = true
    isLoadingTaskDetail.value = true

    try {
      selectedTask.value = await taskQueryService.getDetail(activeProjectId.value, taskId)
    } catch {
      taskDetailError.value = '無法載入任務詳細資訊，請重新整理後再試。'
    } finally {
      isLoadingTaskDetail.value = false
    }
  }

  async function selectProject(projectId: string) {
    activeProjectId.value = projectId
    selectedTask.value = null
    taskDetailError.value = ''
    isTaskDetailOpen.value = false
    await loadBoard(projectId)
  }

  function closeTaskDetail() {
    isTaskDetailOpen.value = false
    selectedTask.value = null
    taskDetailError.value = ''
  }

  async function moveTaskToState(taskId: string, stateKey: WorkflowKey) {
    if (!activeProjectId.value) {
      return
    }

    pageError.value = ''

    try {
      const taskCommandService = new TaskCommandService()
      const updatedTask = await taskCommandService.changeState(activeProjectId.value, taskId, stateKey)

      if (selectedTask.value?.id === taskId) {
        selectedTask.value = updatedTask
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
    isLoadingProjects.value = true
    pageError.value = ''

    try {
      const projectList = await projectQueryService.getProjects()
      projects.value = projectList.items

      if (projects.value.length === 0) {
        activeProjectId.value = null
        activeBoard.value = null
        selectedTask.value = null
        taskDetailError.value = ''
        isTaskDetailOpen.value = false
        return
      }

      const nextProjectId = preferredProjectId && projects.value.some((project) => project.id === preferredProjectId)
        ? preferredProjectId
        : activeProjectId.value && projects.value.some((project) => project.id === activeProjectId.value)
          ? activeProjectId.value
          : projects.value[0].id

      activeProjectId.value = nextProjectId
      await loadBoard(nextProjectId)
    } catch {
      pageError.value = '無法載入專案列表，請確認後端 API 已啟動。'
    } finally {
      isLoadingProjects.value = false
    }
  }

  async function loadBoard(projectId: string) {
    isLoadingBoard.value = true
    pageError.value = ''

    try {
      activeBoard.value = await projectQueryService.getBoard(projectId)
    } catch (error) {
      activeBoard.value = null

      if (error instanceof ApiRequestError && error.status === 404) {
        pageError.value = '找不到指定的專案看板，請重新整理列表。'
        await loadProjects()
        return
      }

      pageError.value = '無法載入專案看板，請稍後再試。'
    } finally {
      isLoadingBoard.value = false
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