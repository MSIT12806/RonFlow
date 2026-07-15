import { computed, onScopeDispose, ref, watch } from 'vue'
import {
  ApiRequestError,
  ApiValidationError,
  type BoardColumnResponse,
  type BoardTaskCardResponse,
  type LifecycleTaskListItemResponse,
  type ProjectListItemResponse,
  type TaskCodeTraceabilityChangeType,
  type TaskEstimatedEffortResponse,
  type TaskLifecycleState,
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

export type TaskDetailMode = 'active' | 'archived' | 'trashed'

export type OpenTaskDetailOptions = {
  forceReadOnly?: boolean
}

export type EditableTaskSubtask = {
  id: string | null
  title: string
  isChecked: boolean
  order: number
}

export type EditableTaskCodeTraceabilityItem = {
  changeType: TaskCodeTraceabilityChangeType
  target: string
}

export type EditableTaskCodeTraceability = {
  api: EditableTaskCodeTraceabilityItem[]
  frontendPages: EditableTaskCodeTraceabilityItem[]
  frontendComponents: EditableTaskCodeTraceabilityItem[]
}

const CONTENT_EDIT_LOCK_INACTIVITY_MS = 30_000
const contentEditActivityEvents = ['pointerdown', 'keydown', 'scroll'] as const

export function useRonFlowBoard() {
  const activeProjectId = ref<string | null>(null)
  const isTaskDetailOpen = ref(false)
  const taskDetailMode = ref<TaskDetailMode>('active')
  const isTaskDetailReadOnly = ref(false)
  const isEditingTaskDetail = ref(false)
  const pendingTaskTitle = ref('')
  let contentEditInactivityTimer: ReturnType<typeof window.setTimeout> | null = null
  let stopTrackingContentEditActivity: (() => void) | null = null

  const pageError = ref('')
  const boardCommandError = ref('')
  const taskLifecycleCommandError = ref('')
  const reminderDateTimeValidationError = ref('')

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

  const archivedTasksResource = useApiResource(
    (projectId: string) => taskQueryService.getArchived(projectId),
    {
      clearDataOnExecute: true,
      createInitialData: () => ({ items: [] as LifecycleTaskListItemResponse[] }),
      mapErrorMessage: () => '無法載入已封存任務，請稍後再試。',
    },
  )

  const trashedTasksResource = useApiResource(
    (projectId: string) => taskQueryService.getTrashed(projectId),
    {
      clearDataOnExecute: true,
      createInitialData: () => ({ items: [] as LifecycleTaskListItemResponse[] }),
      mapErrorMessage: () => '無法載入垃圾桶任務，請稍後再試。',
    },
  )

  const changeTaskStateResource = useApiResource(
    (projectId: string, taskId: string, stateKey: WorkflowKey) =>
      taskCommandService.changeState(projectId, taskId, stateKey),
  )

  const updateTaskDetailResource = useApiResource(
    (
      projectId: string,
      taskId: string,
      title: string,
      description: string,
      dueDate: string | null,
      estimatedEffort: TaskEstimatedEffortResponse | null,
      isShort: boolean,
      codeTraceability: EditableTaskCodeTraceability,
    ) => taskCommandService.update(projectId, taskId, title, description, dueDate, estimatedEffort, isShort, codeTraceability),
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

  const replaceTaskSubtasksResource = useApiResource(
    (
      projectId: string,
      taskId: string,
      subtasks: Array<{ id?: string | null; title: string; isChecked: boolean; order?: number | null }>,
    ) => taskCommandService.replaceSubtasks(projectId, taskId, subtasks),
    {
      mapErrorMessage: (error) => {
        if (error instanceof ApiValidationError) {
          return error.errors.items?.[0] ?? '完成條件標題為必填欄位'
        }

        if (error instanceof ApiRequestError && error.status === 409) {
          return '目前有其他使用者正在編輯此任務，暫時無法更新完成條件。'
        }

        if (error instanceof ApiRequestError && error.status === 404) {
          return '找不到指定的任務，請重新整理專案看板。'
        }

        return '更新完成條件失敗，請稍後再試。'
      },
    },
  )

  const createChildTaskResource = useApiResource(
    (projectId: string, parentTaskId: string, title: string) =>
      taskCommandService.createChild(projectId, parentTaskId, title),
    {
      mapErrorMessage: (error) => {
        if (error instanceof ApiValidationError) {
          return ''
        }

        if (error instanceof ApiRequestError && error.status === 409) {
          return '目前有其他使用者正在編輯此任務，暫時無法建立 child task。'
        }

        if (error instanceof ApiRequestError && error.status === 404) {
          return '找不到指定的任務，請重新整理專案看板。'
        }

        return '建立 child task 失敗，請稍後再試。'
      },
    },
  )

  const setTaskSplitCompleteResource = useApiResource(
    (projectId: string, taskId: string, isSplitComplete: boolean) =>
      taskCommandService.setSplitComplete(projectId, taskId, isSplitComplete),
    {
      mapErrorMessage: (error) => {
        if (error instanceof ApiValidationError) {
          return error.errors.isSplitComplete?.[0] ?? '拆解完成狀態無效'
        }

        if (error instanceof ApiRequestError && error.status === 409) {
          return '目前有其他使用者正在編輯此任務，暫時無法更新拆解狀態。'
        }

        if (error instanceof ApiRequestError && error.status === 404) {
          return '找不到指定的任務，請重新整理專案看板。'
        }

        return '更新拆解狀態失敗，請稍後再試。'
      },
    },
  )

  const reorderTaskResource = useApiResource(
    (projectId: string, taskId: string, targetTaskId: string) =>
      taskCommandService.reorder(projectId, taskId, targetTaskId),
  )

  const moveTaskInTreeResource = useApiResource(
    (
      projectId: string,
      taskId: string,
      payload: { targetParentTaskId: string | null; targetSiblingTaskId: string | null; insertAfter: boolean },
    ) => taskCommandService.moveInTree(projectId, taskId, payload),
  )

  const duplicateTaskSubtreeResource = useApiResource(
    (projectId: string, taskId: string) => taskCommandService.duplicateSubtree(projectId, taskId),
    {
      mapErrorMessage: () => '複製任務樹失敗，請稍後再試。',
    },
  )

  const createTaskReminderResource = useApiResource(
    (projectId: string, taskId: string, reminderDateTime: string, description: string) =>
      taskCommandService.createReminder(projectId, taskId, reminderDateTime, description),
    {
      mapErrorMessage: () => '新增提醒失敗，請稍後再試。',
    },
  )

  const deleteTaskReminderResource = useApiResource(
    (projectId: string, taskId: string, reminderId: string) =>
      taskCommandService.deleteReminder(projectId, taskId, reminderId),
    {
      mapErrorMessage: () => '刪除提醒失敗，請稍後再試。',
    },
  )

  const archiveTaskResource = useApiResource(
    (projectId: string, taskId: string) => taskCommandService.archive(projectId, taskId),
  )

  const restoreArchivedTaskResource = useApiResource(
    (projectId: string, taskId: string) => taskCommandService.restoreArchived(projectId, taskId),
  )

  const moveTaskToTrashResource = useApiResource(
    (projectId: string, taskId: string) => taskCommandService.moveToTrash(projectId, taskId),
  )

  const restoreTrashedTaskResource = useApiResource(
    (projectId: string, taskId: string) => taskCommandService.restoreTrashed(projectId, taskId),
  )

  const projects = computed(() => projectsResource.data.value?.items ?? [])
  const activeBoard = computed(() => boardResource.data.value)
  const selectedTask = computed(() => taskDetailResource.data.value)
  const taskDetailDisplayTitle = computed(() => taskDetailResource.data.value?.title ?? pendingTaskTitle.value)
  const taskDetailError = computed(() => taskDetailResource.errorMessage.value)
  const taskDetailCommandError = computed(() =>
    updateTaskDetailResource.errorMessage.value
    || replaceTaskSubtasksResource.errorMessage.value
    || createChildTaskResource.errorMessage.value
    || setTaskSplitCompleteResource.errorMessage.value
    || createTaskReminderResource.errorMessage.value
    || deleteTaskReminderResource.errorMessage.value
    || taskLifecycleCommandError.value,
  )
  const isLoadingProjects = computed(() => projectsResource.isLoading.value)
  const isLoadingBoard = computed(() => boardResource.isLoading.value)
  const isLoadingTaskDetail = computed(() => taskDetailResource.isLoading.value)
  const isUpdatingTaskDetail = computed(() =>
    updateTaskDetailResource.isLoading.value
    || replaceTaskSubtasksResource.isLoading.value
    || createChildTaskResource.isLoading.value
    || setTaskSplitCompleteResource.isLoading.value
    || duplicateTaskSubtreeResource.isLoading.value
    || createTaskReminderResource.isLoading.value
    || deleteTaskReminderResource.isLoading.value
    || archiveTaskResource.isLoading.value
    || restoreArchivedTaskResource.isLoading.value
    || moveTaskToTrashResource.isLoading.value
    || restoreTrashedTaskResource.isLoading.value,
  )
  const archivedTasks = computed(() => archivedTasksResource.data.value?.items ?? [])
  const trashedTasks = computed(() => trashedTasksResource.data.value?.items ?? [])
  const archivedTasksError = computed(() => archivedTasksResource.errorMessage.value)
  const trashedTasksError = computed(() => trashedTasksResource.errorMessage.value)
  const isLoadingArchivedTasks = computed(() => archivedTasksResource.isLoading.value)
  const isLoadingTrashedTasks = computed(() => trashedTasksResource.isLoading.value)
  const taskTitleValidationError = ref('')

  const activeProject = computed(() =>
    projects.value.find((project) => project.id === activeProjectId.value) ?? null,
  )

  const activeColumns = computed<BoardColumnResponse[]>(() => activeBoard.value?.columns ?? [])
  const activeTaskTree = computed<BoardTaskCardResponse[]>(() => activeBoard.value?.taskTree ?? [])

  function setSelectedTaskCanEnterEdit(canEnterEdit: boolean) {
    if (!selectedTask.value) {
      return
    }

    taskDetailResource.setData({
      ...selectedTask.value,
      canEnterEdit,
    })
  }

  function resolveTaskDetailMode(lifecycleState: TaskLifecycleState): TaskDetailMode {
    switch (lifecycleState) {
      case 'archived':
        return 'archived'
      case 'trashed':
        return 'trashed'
      default:
        return 'active'
    }
  }

  function clearContentEditInactivityTimer() {
    if (contentEditInactivityTimer !== null) {
      window.clearTimeout(contentEditInactivityTimer)
      contentEditInactivityTimer = null
    }
  }

  async function releaseContentEditLockDueToInactivity() {
    if (!activeProjectId.value || !selectedTask.value || !isEditingTaskDetail.value) {
      return
    }

    const projectId = activeProjectId.value
    const taskId = selectedTask.value.id

    try {
      await taskCommandService.releaseContentEditLock(projectId, taskId)
    } catch {}

    isEditingTaskDetail.value = false
    updateTaskDetailResource.reset()
    taskTitleValidationError.value = ''
    reminderDateTimeValidationError.value = ''

    try {
      const task = await taskQueryService.getDetail(projectId, taskId)
      taskDetailResource.setData(task)
      taskDetailMode.value = resolveTaskDetailMode(task.lifecycleState)
    } catch {}
  }

  function resetContentEditInactivityTimer() {
    clearContentEditInactivityTimer()

    if (!isEditingTaskDetail.value) {
      return
    }

    contentEditInactivityTimer = window.setTimeout(() => {
      void releaseContentEditLockDueToInactivity()
    }, CONTENT_EDIT_LOCK_INACTIVITY_MS)
  }

  function stopContentEditActivityTracking() {
    clearContentEditInactivityTimer()

    if (stopTrackingContentEditActivity) {
      stopTrackingContentEditActivity()
      stopTrackingContentEditActivity = null
    }
  }

  function startContentEditActivityTracking() {
    if (typeof window === 'undefined') {
      return
    }

    stopContentEditActivityTracking()

    const onActivity = () => {
      resetContentEditInactivityTimer()
    }

    for (const eventName of contentEditActivityEvents) {
      window.addEventListener(eventName, onActivity, { passive: true })
    }

    stopTrackingContentEditActivity = () => {
      for (const eventName of contentEditActivityEvents) {
        window.removeEventListener(eventName, onActivity)
      }
    }

    resetContentEditInactivityTimer()
  }

  async function openTaskDetail(taskId: string, mode: TaskDetailMode = 'active', taskTitle = '', options: OpenTaskDetailOptions = {}) {
    if (!activeProjectId.value) {
      return
    }

    taskDetailResource.reset()
    updateTaskDetailResource.reset()
    replaceTaskSubtasksResource.reset()
    createChildTaskResource.reset()
    setTaskSplitCompleteResource.reset()
    createTaskReminderResource.reset()
    deleteTaskReminderResource.reset()
    taskLifecycleCommandError.value = ''
    taskTitleValidationError.value = ''
    reminderDateTimeValidationError.value = ''
    taskDetailMode.value = mode
    isTaskDetailReadOnly.value = Boolean(options.forceReadOnly)
    isEditingTaskDetail.value = false
    pendingTaskTitle.value = taskTitle
    isTaskDetailOpen.value = true

    try {
      const task = await taskDetailResource.execute(activeProjectId.value, taskId)
      taskDetailMode.value = resolveTaskDetailMode(task.lifecycleState)
    } catch {}
  }

  async function selectProject(projectId: string) {
    activeProjectId.value = projectId
    taskDetailResource.reset()
    updateTaskDetailResource.reset()
    replaceTaskSubtasksResource.reset()
    createChildTaskResource.reset()
    setTaskSplitCompleteResource.reset()
    createTaskReminderResource.reset()
    deleteTaskReminderResource.reset()
    archivedTasksResource.reset()
    trashedTasksResource.reset()
    taskLifecycleCommandError.value = ''
    taskTitleValidationError.value = ''
    reminderDateTimeValidationError.value = ''
    taskDetailMode.value = 'active'
    isTaskDetailReadOnly.value = false
    isEditingTaskDetail.value = false
    pendingTaskTitle.value = ''
    isTaskDetailOpen.value = false
    await loadBoard(projectId)
  }

  function closeTaskDetail() {
    if (isEditingTaskDetail.value && activeProjectId.value && selectedTask.value) {
      void taskCommandService.releaseContentEditLock(activeProjectId.value, selectedTask.value.id).catch(() => {})
    }

    isTaskDetailOpen.value = false
    taskDetailResource.reset()
    updateTaskDetailResource.reset()
    replaceTaskSubtasksResource.reset()
    createChildTaskResource.reset()
    setTaskSplitCompleteResource.reset()
    createTaskReminderResource.reset()
    deleteTaskReminderResource.reset()
    taskLifecycleCommandError.value = ''
    taskTitleValidationError.value = ''
    reminderDateTimeValidationError.value = ''
    taskDetailMode.value = 'active'
    isTaskDetailReadOnly.value = false
    isEditingTaskDetail.value = false
    pendingTaskTitle.value = ''
  }

  function enterTaskDetailEditMode() {
    if (isTaskDetailReadOnly.value || taskDetailMode.value !== 'active' || !selectedTask.value || !activeProjectId.value) {
      return
    }

    void taskCommandService.acquireContentEditLock(activeProjectId.value, selectedTask.value.id)
      .then((task) => {
        taskDetailResource.setData(task)
        isEditingTaskDetail.value = true
      })
      .catch((error) => {
        if (error instanceof ApiRequestError && error.status === 409) {
          setSelectedTaskCanEnterEdit(false)
        }
      })
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

      await refreshBoardSilently(activeProjectId.value)
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 404) {
        boardCommandError.value = '找不到指定的任務，請重新整理專案看板。'
        return
      }

      boardCommandError.value = '變更任務狀態失敗，請稍後再試。'
    }
  }

  async function updateTaskDetail(
    taskId: string,
    title: string,
    description: string,
    dueDate: string | null,
    estimatedEffort: TaskEstimatedEffortResponse | null,
    isShort: boolean,
    codeTraceability: EditableTaskCodeTraceability,
    subtasks: EditableTaskSubtask[],
  ) {
    if (!activeProjectId.value) {
      return
    }

    taskTitleValidationError.value = ''
    updateTaskDetailResource.reset()
    replaceTaskSubtasksResource.reset()
    createChildTaskResource.reset()
    setTaskSplitCompleteResource.reset()
    taskLifecycleCommandError.value = ''

    try {
      let updatedTask = await updateTaskDetailResource.execute(
        activeProjectId.value,
        taskId,
        title,
        description,
        dueDate,
        estimatedEffort,
        isShort,
        codeTraceability,
      )
      updatedTask = await replaceTaskSubtasksResource.execute(
        activeProjectId.value,
        taskId,
        subtasks.map((subtask, index) => ({
          id: subtask.id,
          title: subtask.title,
          isChecked: subtask.isChecked,
          order: index,
        })),
      )

      if (isEditingTaskDetail.value) {
        try {
          await taskCommandService.releaseContentEditLock(activeProjectId.value, taskId)
        } catch {}
      }

      taskDetailResource.setData({
        ...updatedTask,
        canEnterEdit: true,
      })
      isEditingTaskDetail.value = false
      await loadBoard(activeProjectId.value)
    } catch (error) {
      if (error instanceof ApiValidationError) {
        taskTitleValidationError.value = error.errors.title?.[0] ?? '任務標題為必填欄位'
      }
    }
  }

  async function sendTaskToFlow(taskId: string) {
    const initialStateKey = activeColumns.value.find((column) => column.isInitialState)?.stateKey ?? 'todo'
    await moveTaskToState(taskId, initialStateKey)
  }

  async function replaceTaskSubtasks(taskId: string, subtasks: EditableTaskSubtask[]) {
    if (!activeProjectId.value) {
      return false
    }

    replaceTaskSubtasksResource.reset()

    try {
      const updatedTask = await replaceTaskSubtasksResource.execute(
        activeProjectId.value,
        taskId,
        subtasks.map((subtask, index) => ({
          id: subtask.id,
          title: subtask.title,
          isChecked: subtask.isChecked,
          order: index,
        })),
      )

      taskDetailResource.setData({
        ...updatedTask,
        canEnterEdit: true,
      })
      await loadBoard(activeProjectId.value)
      return true
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 409) {
        setSelectedTaskCanEnterEdit(false)
      }

      try {
        const task = await taskQueryService.getDetail(activeProjectId.value, taskId)
        taskDetailResource.setData(task)
        taskDetailMode.value = resolveTaskDetailMode(task.lifecycleState)
      } catch {}

      return false
    }
  }

  async function setTaskSplitComplete(taskId: string, isSplitComplete: boolean) {
    if (!activeProjectId.value) {
      return false
    }

    setTaskSplitCompleteResource.reset()

    try {
      await setTaskSplitCompleteResource.execute(activeProjectId.value, taskId, isSplitComplete)
      const task = await taskQueryService.getDetail(activeProjectId.value, taskId)
      taskDetailResource.setData(task)
      taskDetailMode.value = resolveTaskDetailMode(task.lifecycleState)
      await loadBoard(activeProjectId.value)
      return true
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 409) {
        setSelectedTaskCanEnterEdit(false)
      }

      try {
        const task = await taskQueryService.getDetail(activeProjectId.value, taskId)
        taskDetailResource.setData(task)
        taskDetailMode.value = resolveTaskDetailMode(task.lifecycleState)
      } catch {}

      return false
    }
  }

  async function createChildTask(parentTaskId: string, title: string) {
    if (!activeProjectId.value) {
      return false
    }

    taskTitleValidationError.value = ''
    createChildTaskResource.reset()

    try {
      await createChildTaskResource.execute(activeProjectId.value, parentTaskId, title)
      const parentTask = await taskQueryService.getDetail(activeProjectId.value, parentTaskId)
      taskDetailResource.setData(parentTask)
      await loadBoard(activeProjectId.value)
      return true
    } catch (error) {
      if (error instanceof ApiValidationError) {
        taskTitleValidationError.value = error.errors.title?.[0] ?? '任務標題為必填欄位'
      }

      if (error instanceof ApiRequestError && error.status === 409) {
        setSelectedTaskCanEnterEdit(false)
      }

      return false
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

  async function moveTaskWithinTree(
    taskId: string,
    payload: { targetParentTaskId: string | null; targetSiblingTaskId: string | null; insertAfter: boolean },
  ) {
    if (!activeProjectId.value) {
      return
    }

    boardCommandError.value = ''

    try {
      const updatedTask = await moveTaskInTreeResource.execute(activeProjectId.value, taskId, payload)

      if (selectedTask.value?.id === taskId) {
        taskDetailResource.setData(updatedTask)
      }

      await loadBoard(activeProjectId.value)
    } catch (error) {
      if (error instanceof ApiValidationError) {
        boardCommandError.value = error.errors.targetParentTaskId?.[0]
          ?? error.errors.targetSiblingTaskId?.[0]
          ?? '調整任務樹位置失敗，請重新整理後再試。'
        return
      }

      if (error instanceof ApiRequestError && error.status === 404) {
        boardCommandError.value = '找不到指定的任務，請重新整理專案看板。'
        return
      }

      boardCommandError.value = '調整任務樹位置失敗，請稍後再試。'
    }
  }

  async function duplicateTaskSubtree(taskId: string) {
    if (!activeProjectId.value) {
      return false
    }

    try {
      await duplicateTaskSubtreeResource.execute(activeProjectId.value, taskId)
      await loadBoard(activeProjectId.value)
      return true
    } catch {
      return false
    }
  }

  async function createReminder(taskId: string, reminderDateTime: string, description: string) {
    if (!activeProjectId.value) {
      return false
    }

    reminderDateTimeValidationError.value = ''
    createTaskReminderResource.reset()
    deleteTaskReminderResource.reset()

    if (!reminderDateTime.trim()) {
      reminderDateTimeValidationError.value = '提醒時間為必填欄位'
      return false
    }

    try {
      const updatedTask = await createTaskReminderResource.execute(activeProjectId.value, taskId, reminderDateTime, description)
      taskDetailResource.setData(updatedTask)
      await loadBoard(activeProjectId.value)
      return true
    } catch {
      return false
    }
  }

  async function deleteReminder(taskId: string, reminderId: string) {
    if (!activeProjectId.value) {
      return false
    }

    createTaskReminderResource.reset()
    deleteTaskReminderResource.reset()

    try {
      const updatedTask = await deleteTaskReminderResource.execute(activeProjectId.value, taskId, reminderId)
      taskDetailResource.setData(updatedTask)
      await loadBoard(activeProjectId.value)
      return true
    } catch {
      return false
    }
  }

  async function loadArchivedTasks(projectId = activeProjectId.value) {
    if (!projectId) {
      archivedTasksResource.reset()
      return
    }

    try {
      await archivedTasksResource.execute(projectId)
    } catch {}
  }

  async function loadTrashedTasks(projectId = activeProjectId.value) {
    if (!projectId) {
      trashedTasksResource.reset()
      return
    }

    try {
      await trashedTasksResource.execute(projectId)
    } catch {}
  }

  async function archiveTask(taskId: string) {
    if (!activeProjectId.value) {
      return false
    }

    taskLifecycleCommandError.value = ''

    try {
      await archiveTaskResource.execute(activeProjectId.value, taskId)
      closeTaskDetail()
      await Promise.all([loadBoard(activeProjectId.value), loadArchivedTasks(activeProjectId.value)])
      return true
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 404) {
        taskLifecycleCommandError.value = '找不到指定的任務，請重新整理專案看板。'
        return false
      }

      taskLifecycleCommandError.value = '封存任務失敗，請稍後再試。'
      return false
    }
  }

  async function moveTaskIntoTrash(taskId: string) {
    if (!activeProjectId.value) {
      return false
    }

    taskLifecycleCommandError.value = ''

    try {
      await moveTaskToTrashResource.execute(activeProjectId.value, taskId)
      closeTaskDetail()
      await Promise.all([loadBoard(activeProjectId.value), loadTrashedTasks(activeProjectId.value)])
      return true
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 404) {
        taskLifecycleCommandError.value = '找不到指定的任務，請重新整理專案看板。'
        return false
      }

      taskLifecycleCommandError.value = '移到垃圾桶失敗，請稍後再試。'
      return false
    }
  }

  async function restoreArchivedTask(taskId: string) {
    if (!activeProjectId.value) {
      return false
    }

    taskLifecycleCommandError.value = ''

    try {
      await restoreArchivedTaskResource.execute(activeProjectId.value, taskId)
      closeTaskDetail()
      await Promise.all([loadBoard(activeProjectId.value), loadArchivedTasks(activeProjectId.value)])
      return true
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 404) {
        taskLifecycleCommandError.value = '找不到指定的任務，請重新整理任務列表。'
        return false
      }

      taskLifecycleCommandError.value = '還原封存任務失敗，請稍後再試。'
      return false
    }
  }

  async function restoreTrashedTask(taskId: string) {
    if (!activeProjectId.value) {
      return false
    }

    taskLifecycleCommandError.value = ''

    try {
      await restoreTrashedTaskResource.execute(activeProjectId.value, taskId)
      closeTaskDetail()
      await Promise.all([loadBoard(activeProjectId.value), loadTrashedTasks(activeProjectId.value)])
      return true
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 404) {
        taskLifecycleCommandError.value = '找不到指定的任務，請重新整理任務列表。'
        return false
      }

      taskLifecycleCommandError.value = '從垃圾桶還原任務失敗，請稍後再試。'
      return false
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
        archivedTasksResource.reset()
        trashedTasksResource.reset()
        isTaskDetailOpen.value = false
        isEditingTaskDetail.value = false
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

  async function refreshProjectsSilently(preferredProjectId = activeProjectId.value ?? undefined) {
    try {
      const projectList = await projectQueryService.getProjects()
      projectsResource.setData(projectList)

      if (projectList.items.length === 0) {
        activeProjectId.value = null
        boardResource.reset()
        archivedTasksResource.reset()
        trashedTasksResource.reset()
        isTaskDetailOpen.value = false
        isEditingTaskDetail.value = false
        return
      }

      const nextProjectId = preferredProjectId && projectList.items.some((project) => project.id === preferredProjectId)
        ? preferredProjectId
        : activeProjectId.value && projectList.items.some((project) => project.id === activeProjectId.value)
          ? activeProjectId.value
          : projectList.items[0].id

      activeProjectId.value = nextProjectId
    } catch {}
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

  async function refreshBoardSilently(projectId = activeProjectId.value) {
    if (!projectId) {
      return
    }

    try {
      const board = await projectQueryService.getBoard(projectId)
      boardResource.setData(board)
    } catch {}
  }

  async function refreshSelectedTaskDetailSilently() {
    if (!activeProjectId.value || !isTaskDetailOpen.value || isEditingTaskDetail.value || !selectedTask.value) {
      return
    }

    try {
      const task = await taskQueryService.getDetail(activeProjectId.value, selectedTask.value.id)
      taskDetailResource.setData(task)
      taskDetailMode.value = resolveTaskDetailMode(task.lifecycleState)
    } catch {}
  }



  watch(isEditingTaskDetail, (isEditing) => {
    if (isEditing) {
      startContentEditActivityTracking()
      return
    }

    stopContentEditActivityTracking()
  })

  onScopeDispose(() => {
    stopContentEditActivityTracking()
  })

  return {
    projects,
    activeProjectId,
    activeProject,
    activeColumns,
    activeTaskTree,
    selectedTask,
    taskDetailDisplayTitle,
    taskDetailMode,
    isTaskDetailReadOnly,
    isEditingTaskDetail,
    archivedTasks,
    trashedTasks,
    isTaskDetailOpen,
    taskDetailError,
    taskDetailCommandError,
    isLoadingProjects,
    isLoadingBoard,
    isLoadingTaskDetail,
    isUpdatingTaskDetail,
    isLoadingArchivedTasks,
    isLoadingTrashedTasks,
    archivedTasksError,
    trashedTasksError,
    pageError,
    boardCommandError,
    taskTitleValidationError,
    reminderDateTimeValidationError,
    openTaskDetail,
    enterTaskDetailEditMode,
    selectProject,
    closeTaskDetail,
    moveTaskToState,
    updateTaskDetail,
    sendTaskToFlow,
    replaceTaskSubtasks,
    setTaskSplitComplete,
    createChildTask,
    createReminder,
    deleteReminder,
    reorderTaskWithinColumn,
    moveTaskWithinTree,
    duplicateTaskSubtree,
    loadArchivedTasks,
    loadTrashedTasks,
    archiveTask,
    moveTaskIntoTrash,
    restoreArchivedTask,
    restoreTrashedTask,
    loadProjects,
    refreshProjectsSilently,
    loadBoard,
    refreshBoardSilently,
    refreshSelectedTaskDetailSilently,
    formatProjectMeta,
    formatTimelineTime,
  }
}
