import { computed, onMounted, ref } from 'vue'
import {
  ApiRequestError,
  ApiValidationError,
  changeTaskState,
  createProject,
  createTask,
  getProjectBoard,
  getProjects,
  getTaskDetail,
  type ProjectBoardResponse,
  type ProjectListItemResponse,
  type TaskDetailResponse,
  type WorkflowKey,
} from '../api/ronflowApi'

const workflowColumns: Array<{ key: WorkflowKey; label: string }> = [
  { key: 'todo', label: '待處理' },
  { key: 'active', label: '進行中' },
  { key: 'review', label: '審查中' },
  { key: 'done', label: '已完成' },
]

export function useRonFlowBoard() {
  const projects = ref<ProjectListItemResponse[]>([])
  const activeProjectId = ref<string | null>(null)
  const activeBoard = ref<ProjectBoardResponse | null>(null)
  const selectedTask = ref<TaskDetailResponse | null>(null)

  const isCreateProjectOpen = ref(false)
  const isCreateTaskOpen = ref(false)
  const isTaskDetailOpen = ref(false)
  const isLoadingProjects = ref(false)
  const isLoadingBoard = ref(false)
  const isSubmittingProject = ref(false)
  const isSubmittingTask = ref(false)
  const isLoadingTaskDetail = ref(false)

  const projectNameInput = ref('')
  const taskTitleInput = ref('')
  const projectNameError = ref('')
  const taskTitleError = ref('')
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

  function openCreateProjectModal() {
    projectNameInput.value = ''
    projectNameError.value = ''
    isCreateProjectOpen.value = true
  }

  function closeCreateProjectModal() {
    isCreateProjectOpen.value = false
  }

  async function submitProject() {
    projectNameError.value = ''
    pageError.value = ''
    isSubmittingProject.value = true

    try {
      const project = await createProject(projectNameInput.value)
      closeCreateProjectModal()
      await loadProjects(project.id)
    } catch (error) {
      handleProjectCreationError(error)
    } finally {
      isSubmittingProject.value = false
    }
  }

  function openCreateTaskModal() {
    if (!activeProject.value) {
      return
    }

    taskTitleInput.value = ''
    taskTitleError.value = ''
    isCreateTaskOpen.value = true
  }

  function closeCreateTaskModal() {
    isCreateTaskOpen.value = false
  }

  async function submitTask() {
    taskTitleError.value = ''
    pageError.value = ''

    if (!activeProjectId.value) {
      return
    }

    isSubmittingTask.value = true

    try {
      await createTask(activeProjectId.value, taskTitleInput.value)
      closeCreateTaskModal()
      await Promise.all([loadProjects(activeProjectId.value), loadBoard(activeProjectId.value)])
    } catch (error) {
      handleTaskCreationError(error)
    } finally {
      isSubmittingTask.value = false
    }
  }

  async function openTaskDetail(taskId: string) {
    if (!activeProjectId.value) {
      return
    }

    pageError.value = ''
    isLoadingTaskDetail.value = true

    try {
      selectedTask.value = await getTaskDetail(activeProjectId.value, taskId)
      isTaskDetailOpen.value = true
    } catch {
      pageError.value = '無法載入任務詳細資訊，請重新整理後再試。'
    } finally {
      isLoadingTaskDetail.value = false
    }
  }

  async function selectProject(projectId: string) {
    activeProjectId.value = projectId
    selectedTask.value = null
    isTaskDetailOpen.value = false
    await loadBoard(projectId)
  }

  function closeTaskDetail() {
    isTaskDetailOpen.value = false
    selectedTask.value = null
  }

  async function moveTaskToDone(taskId: string) {
    if (!activeProjectId.value) {
      return
    }

    pageError.value = ''

    try {
      const updatedTask = await changeTaskState(activeProjectId.value, taskId, 'done')

      if (selectedTask.value?.id === taskId) {
        selectedTask.value = updatedTask
      }

      await loadProjects(activeProjectId.value)
    } catch (error) {
      handleTaskStateChangeError(error)
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
      const projectList = await getProjects()
      projects.value = projectList.items

      if (projects.value.length === 0) {
        activeProjectId.value = null
        activeBoard.value = null
        selectedTask.value = null
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
      activeBoard.value = await getProjectBoard(projectId)
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

  function handleProjectCreationError(error: unknown) {
    if (error instanceof ApiValidationError) {
      projectNameError.value = error.errors.name?.[0] ?? '專案名稱為必填欄位'
      return
    }

    pageError.value = '建立專案失敗，請稍後再試。'
  }

  function handleTaskCreationError(error: unknown) {
    if (error instanceof ApiValidationError) {
      taskTitleError.value = error.errors.title?.[0] ?? '任務標題為必填欄位'
      return
    }

    if (error instanceof ApiRequestError && error.status === 404) {
      pageError.value = '目前專案不存在，請重新整理專案列表。'
      return
    }

    pageError.value = '建立任務失敗，請稍後再試。'
  }

  function handleTaskStateChangeError(error: unknown) {
    if (error instanceof ApiRequestError && error.status === 404) {
      pageError.value = '找不到指定的任務，請重新整理專案看板。'
      return
    }

    pageError.value = '變更任務狀態失敗，請稍後再試。'
  }

  return {
    projects,
    activeProjectId,
    activeProject,
    activeColumns,
    selectedTask,
    workflowColumns,
    isCreateProjectOpen,
    isCreateTaskOpen,
    isTaskDetailOpen,
    isLoadingProjects,
    isLoadingBoard,
    isSubmittingProject,
    isSubmittingTask,
    isLoadingTaskDetail,
    projectNameInput,
    taskTitleInput,
    projectNameError,
    taskTitleError,
    pageError,
    openCreateProjectModal,
    closeCreateProjectModal,
    submitProject,
    openCreateTaskModal,
    closeCreateTaskModal,
    submitTask,
    openTaskDetail,
    selectProject,
    closeTaskDetail,
    moveTaskToDone,
    getTasksByStatus,
    formatProjectMeta,
    formatTimelineTime,
  }
}