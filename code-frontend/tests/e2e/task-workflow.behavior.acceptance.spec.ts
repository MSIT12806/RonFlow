import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  createTask,
  createTaskTitle,
  dragTaskToColumn,
  expectTaskOrder,
  getTaskCard,
  openCreateTaskModal,
  openProjectFromList,
  openTaskDetail,
  setupProjectBoard,
  setupTaskBoard,
} from './support/ronflowTestHelpers'
import {
  configureTestFaultsThroughApi,
  createProjectThroughApi,
  createTaskThroughApi,
  moveTaskStateThroughApi,
  registerRonFlowApiUser,
} from './support/ronflowApiTestHelpers'
import { createRonFlowAuthUser, loginAndEnterWorkspace } from './support/ronflowAuthTestHelpers'

async function dragTaskAndWaitForStateResponse(
  page: Parameters<typeof test>[0]['page'],
  fromStateKey: string,
  toStateKey: string,
  taskTitle: string,
) {
  const taskCard = getTaskCard(page, fromStateKey, taskTitle)
  const targetColumn = page.getByTestId(`workflow-column-${toStateKey}`)

  for (let attempt = 0; attempt < 2; attempt += 1) {
    const responsePromise = page.waitForResponse((response) => {
      return response.request().method() === 'PATCH'
        && /\/api\/projects\/[^/]+\/tasks\/[^/]+\/state$/.test(response.url())
    }, { timeout: 3000 }).catch(() => null)

    await taskCard.dragTo(targetColumn)

    const response = await responsePromise
    if (response) {
      return response
    }
  }

  return null
}

async function setupTaskBoardThroughApi(
  request: Parameters<typeof test>[0]['request'],
  page: Parameters<typeof test>[0]['page'],
  projectName: string,
  taskTitle: string,
  taskStates: string[] = [],
) {
  const userSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))
  const project = await createProjectThroughApi(request, userSession, projectName)
  const task = await createTaskThroughApi(request, userSession, project.id, taskTitle)

  for (const stateKey of taskStates) {
    await moveTaskStateThroughApi(request, userSession, project.id, task.id, stateKey)
  }

  await loginAndEnterWorkspace(page, userSession.user)
  await openProjectFromList(page, projectName)

  return { userSession, project, task }
}

test.describe('RonFlow UI/UX 驗收規格 - Task Workflow Behavior', () => {
  test('使用者可以拖曳任務到進行中欄位，並在詳細資訊看到最新狀態', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)

    const taskCard = getTaskCard(page, 'todo', taskTitle)
    const activeColumn = page.getByTestId('workflow-column-active')

    const response = await dragTaskAndWaitForStateResponse(page, 'todo', 'active', taskTitle)

    expect(response?.ok()).toBeTruthy()
    await expect(activeColumn).toContainText(taskTitle)
    await expect(page.getByTestId('workflow-column-todo')).not.toContainText(taskTitle)

    await activeColumn.getByText(taskTitle, { exact: true }).click()

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog.getByText('進行中', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('任務狀態已變更為 進行中', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('完成時間', { exact: true })).toHaveCount(0)
  })

  test('使用者可以將進行中的任務拖曳到已完成欄位並看到完成紀錄', async ({ page, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoardThroughApi(request, page, projectName, taskTitle, ['active'])

    await dragTaskToColumn(page, 'active', 'done', taskTitle)

    const doneColumn = page.getByTestId('workflow-column-done')

    await expect(doneColumn).toContainText(taskTitle)
    await expect(page.getByTestId('workflow-column-active')).not.toContainText(taskTitle)

    await openTaskDetail(page, 'done', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog.getByText('已完成', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('任務狀態已變更為 已完成', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('完成時間', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已完成任務', { exact: true })).toBeVisible()
  })

  test('拖曳後若沒有放到有效欄位，Task Card 會留在原欄位與原位置', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const firstTaskTitle = createTaskTitle(testInfo, 'Backlog Task A')
    const secondTaskTitle = createTaskTitle(testInfo, 'Backlog Task B')

    await setupProjectBoard(page, projectName)

    await openCreateTaskModal(page)
    await createTask(page, firstTaskTitle)

    await openCreateTaskModal(page)
    await createTask(page, secondTaskTitle)

    await expectTaskOrder(page, 'todo', [firstTaskTitle, secondTaskTitle])

    await getTaskCard(page, 'todo', secondTaskTitle).dragTo(page.getByRole('heading', { name: projectName }))

    await expectTaskOrder(page, 'todo', [firstTaskTitle, secondTaskTitle])
    await expect(page.getByTestId('workflow-column-active')).not.toContainText(secondTaskTitle)
  })

  test('狀態變更 API 失敗時，Task Card 會回到原欄位並顯示錯誤訊息', async ({ page, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    const { userSession } = await setupTaskBoardThroughApi(request, page, projectName, taskTitle)

    await configureTestFaultsThroughApi(request, userSession, [{
      method: 'PATCH',
      pathPattern: '/api/projects/*/tasks/*/state',
      statusCode: 500,
      message: 'move failed',
    }])

    const response = await dragTaskAndWaitForStateResponse(page, 'todo', 'active', taskTitle)

    expect(response?.status()).toBe(500)
    await expect(page.getByTestId('workflow-column-todo')).toContainText(taskTitle)
    await expect(page.getByTestId('workflow-column-active')).not.toContainText(taskTitle)
    await expect(page.getByTestId('base-error-state')).toContainText('變更任務狀態失敗，請稍後再試。')
  })

  test('使用者可以將已完成任務重新開啟，清除目前完成時間並記錄重新開啟活動', async ({ page, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoardThroughApi(request, page, projectName, taskTitle, ['active', 'done'])

    await dragTaskToColumn(page, 'done', 'active', taskTitle)

    await expect(page.getByTestId('workflow-column-active')).toContainText(taskTitle)
    await expect(page.getByTestId('workflow-column-done')).not.toContainText(taskTitle)

    await openTaskDetail(page, 'active', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog.getByText('進行中', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('完成時間', { exact: true })).toHaveCount(0)
    await expect(detailDialog.getByText('已完成任務', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已重新開啟任務', { exact: true })).toBeVisible()
  })

  test('使用者可以在同欄位內拖曳調整任務順序，並以新順序反映工作優先順序', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const firstTaskTitle = createTaskTitle(testInfo, 'Priority Task A')
    const secondTaskTitle = createTaskTitle(testInfo, 'Priority Task B')

    await setupProjectBoard(page, projectName)

    await openCreateTaskModal(page)
    await createTask(page, firstTaskTitle)

    await openCreateTaskModal(page)
    await createTask(page, secondTaskTitle)

    await expectTaskOrder(page, 'todo', [firstTaskTitle, secondTaskTitle])

    await getTaskCard(page, 'todo', secondTaskTitle).dragTo(getTaskCard(page, 'todo', firstTaskTitle))

    await expectTaskOrder(page, 'todo', [secondTaskTitle, firstTaskTitle])

    await openTaskDetail(page, 'todo', secondTaskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })
    await expect(detailDialog.getByText('已調整任務順序', { exact: true })).toBeVisible()
  })
})
