import { computed, onMounted, ref } from 'vue'
import {
  ApiRequestError,
  ApiValidationError,
  type BoardColumnResponse,
  type ProjectListItemResponse,
  type WorkflowKey,
} from '../api/ronflowApi'
import {
  ProjectQueryService,
  TaskQueryService,
  TaskCommandService,
} from '../application'
import { useApiResource } from './useApiResource'

const projectQueryService = new ProjectQueryService()
const taskQueryService = new TaskQueryService()
const taskCommandService = new TaskCommandService()

export function useRonFlowBoard() {
  const activeProjectId = ref<string | null>(null)
  const isTaskDetailOpen = ref(false)

  const pageError = ref('')
  const boardCommandError = ref('')

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

  const updateTaskDetailResource = useApiResource(
    (projectId: string, taskId: string, title: string, description: string, dueDate: string | null) =>
      taskCommandService.update(projectId, taskId, title, description, dueDate),
    {
      mapErrorMessage: (error) => {
        if (error instanceof ApiValidationError) {
          return ''
        }

        if (error instanceof ApiRequestError && error.status === 404) {
          return '找不到指定的任務，請重新整理專案看板。'
        }

        return '更新任務失敗，請稍後再試。'
      },
    },
  )

  const reorderTaskResource = useApiResource(
    (projectId: string, taskId: string, targetTaskId: string) =>
      taskCommandService.reorder(projectId, taskId, targetTaskId),
  )

  const projects = computed(() => projectsResource.data.value?.items ?? [])
  const activeBoard = computed(() => boardResource.data.value)
  const selectedTask = computed(() => taskDetailResource.data.value)
  const taskDetailError = computed(() => taskDetailResource.errorMessage.value)
  const taskDetailCommandError = computed(() => updateTaskDetailResource.errorMessage.value)
  const isLoadingProjects = computed(() => projectsResource.isLoading.value)
  const isLoadingBoard = computed(() => boardResource.isLoading.value)
  const isLoadingTaskDetail = computed(() => taskDetailResource.isLoading.value)
  const isUpdatingTaskDetail = computed(() => updateTaskDetailResource.isLoading.value)
  const taskTitleValidationError = ref('')

  const activeProject = computed(() =>
    projects.value.find((project) => project.id === activeProjectId.value) ?? null,
  )

  const activeColumns = computed<BoardColumnResponse[]>(() => activeBoard.value?.columns ?? [])

  onMounted(async () => {
    await loadProjects()
  })



  async function openTaskDetail(taskId: string) {
    if (!activeProjectId.value) {
      return
    }

    taskDetailResource.reset()
    updateTaskDetailResource.reset()
    taskTitleValidationError.value = ''
    isTaskDetailOpen.value = true

    try {
      await taskDetailResource.execute(activeProjectId.value, taskId)
    } catch {}
  }

  async function selectProject(projectId: string) {
    activeProjectId.value = projectId
    taskDetailResource.reset()
    updateTaskDetailResource.reset()
    taskTitleValidationError.value = ''
    isTaskDetailOpen.value = false
    await loadBoard(projectId)
  }

  function closeTaskDetail() {
    isTaskDetailOpen.value = false
    taskDetailResource.reset()
    updateTaskDetailResource.reset()
    taskTitleValidationError.value = ''
  }

  async function moveTaskToState(taskId: string, stateKey: WorkflowKey) {
    if (!activeProjectId.value) {
      return
    }

    boardCommandError.value = ''

    try {
      const updatedTask = await changeTaskStateResource.execute(activeProjectId.value, taskId, stateKey)

      if (selectedTask.value?.id === taskId) {
        taskDetailResource.setData(updatedTask)
      }

      await loadBoard(activeProjectId.value)
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 404) {
        boardCommandError.value = '找不到指定的任務，請重新整理專案看板。'
        return
      }

      boardCommandError.value = '變更任務狀態失敗，請稍後再試。'
    }
  }

  async function updateTaskDetail(taskId: string, title: string, description: string, dueDate: string | null) {
    if (!activeProjectId.value) {
      return
    }

    taskTitleValidationError.value = ''
    updateTaskDetailResource.reset()

    try {
      const updatedTask = await updateTaskDetailResource.execute(activeProjectId.value, taskId, title, description, dueDate)
      taskDetailResource.setData(updatedTask)
      await loadBoard(activeProjectId.value)
    } catch (error) {
      if (error instanceof ApiValidationError) {
        taskTitleValidationError.value = error.errors.title?.[0] ?? '任務標題為必填欄位'
      }
    }
  }

  async function reorderTaskWithinColumn(taskId: string, targetTaskId: string) {
    if (!activeProjectId.value) {
      return
    }

    boardCommandError.value = ''

    try {
      const updatedTask = await reorderTaskResource.execute(activeProjectId.value, taskId, targetTaskId)

      if (selectedTask.value?.id === taskId) {
        taskDetailResource.setData(updatedTask)
      }

      await loadBoard(activeProjectId.value)
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 404) {
        boardCommandError.value = '找不到指定的任務，請重新整理專案看板。'
        return
      }

      boardCommandError.value = '調整任務順序失敗，請稍後再試。'
    }
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
    boardCommandError.value = ''

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
    taskDetailCommandError,
    isLoadingProjects,
    isLoadingBoard,
    isLoadingTaskDetail,
    isUpdatingTaskDetail,
    pageError,
    boardCommandError,
    taskTitleValidationError,
    openTaskDetail,
    selectProject,
    closeTaskDetail,
    moveTaskToState,
    updateTaskDetail,
    reorderTaskWithinColumn,
    loadProjects,
    loadBoard,
    formatProjectMeta,
    formatTimelineTime,
  }
}