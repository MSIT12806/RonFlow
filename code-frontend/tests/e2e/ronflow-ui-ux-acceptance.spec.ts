import { expect, test, type APIRequestContext, type Page, type TestInfo } from '@playwright/test'

const workflowColumns = [
  { key: 'todo', label: '待處理' },
  { key: 'active', label: '進行中' },
  { key: 'review', label: '審查中' },
  { key: 'done', label: '已完成' },
] as const
const backendApiBaseUrl = 'http://127.0.0.1:5078/api'

type ProjectResponse = {
  id: string
}

type TaskResponse = {
  id: string
}

function createScenarioData(testInfo: TestInfo) {
  const suffix = `${testInfo.workerIndex}-${testInfo.retry}-${Date.now()}`

  return {
    projectName: `RonFlow Project ${suffix}`,
    taskTitle: `Build Kanban Board ${suffix}`,
  }
}

async function openCreateProjectModal(page: Page) {
  await page.getByRole('button', { name: '建立專案' }).click()
  await expect(page.getByRole('dialog', { name: '建立專案' })).toBeVisible()
}

async function createProject(page: Page, projectName: string) {
  const dialog = page.getByRole('dialog', { name: '建立專案' })

  await dialog.getByLabel('專案名稱').fill(projectName)
  await dialog.getByRole('button', { name: '建立', exact: true }).click()

  await expect(dialog).not.toBeVisible()
  await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
}

async function openCreateTaskModal(page: Page) {
  await page.getByRole('button', { name: '建立任務' }).click()
  await expect(page.getByRole('dialog', { name: '建立任務' })).toBeVisible()
}

async function createTask(page: Page, taskTitle: string) {
  const dialog = page.getByRole('dialog', { name: '建立任務' })

  await dialog.getByLabel('任務標題').fill(taskTitle)
  await dialog.getByRole('button', { name: '建立', exact: true }).click()

  await expect(dialog).not.toBeVisible()
}

async function openTaskDetail(page: Page, stateKey: string, taskTitle: string) {
  await page.getByTestId(`workflow-column-${stateKey}`).getByText(taskTitle, { exact: true }).click()
  await expect(page.getByRole('dialog', { name: '任務詳細資訊' })).toBeVisible()
}

async function dragTaskToColumn(page: Page, fromStateKey: string, toStateKey: string, taskTitle: string) {
  const taskCard = getTaskCard(page, fromStateKey, taskTitle)
  const sourceColumn = page.getByTestId(`workflow-column-${fromStateKey}`)
  const targetColumn = page.getByTestId(`workflow-column-${toStateKey}`)

  await taskCard.dragTo(targetColumn)
  await expect(targetColumn).toContainText(taskTitle)
  await expect(sourceColumn).not.toContainText(taskTitle)
}

async function createProjectThroughApi(request: APIRequestContext, projectName: string) {
  const response = await request.post(`${backendApiBaseUrl}/projects`, {
    data: { name: projectName },
  })

  expect(response.ok()).toBeTruthy()
  return response.json() as Promise<ProjectResponse>
}

async function createTaskThroughApi(request: APIRequestContext, projectId: string, taskTitle: string) {
  const response = await request.post(`${backendApiBaseUrl}/projects/${projectId}/tasks`, {
    data: { title: taskTitle },
  })

  expect(response.ok()).toBeTruthy()
  return response.json() as Promise<TaskResponse>
}

async function moveTaskStateThroughApi(
  request: APIRequestContext,
  projectId: string,
  taskId: string,
  stateKey: string,
) {
  const response = await request.patch(`${backendApiBaseUrl}/projects/${projectId}/tasks/${taskId}/state`, {
    data: { stateKey },
  })

  expect(response.ok()).toBeTruthy()
}

async function setupProjectBoard(page: Page, projectName: string) {
  await page.goto('/')
  await openCreateProjectModal(page)
  await createProject(page, projectName)
}

async function setupTaskBoard(page: Page, projectName: string, taskTitle: string) {
  await setupProjectBoard(page, projectName)
  await openCreateTaskModal(page)
  await createTask(page, taskTitle)
}

function getTaskCard(page: Page, stateKey: string, taskTitle: string) {
  return page
    .getByTestId(`workflow-column-${stateKey}`)
    .locator('.task-card')
    .filter({ hasText: taskTitle })
    .first()
}

test.describe('RonFlow UI/UX 驗收規格', () => {
  // Project List Page
  test('專案列表首屏顯示建立專案入口與空狀態', async ({ page }) => {
    await page.route('**/api/projects', async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: [] }),
        })
        return
      }

      await route.continue()
    })

    await page.goto('/')

    await expect(page).toHaveTitle(/RonFlow/)
    await expect(page.getByRole('heading', { name: '專案列表' })).toBeVisible()
    await expect(page.getByRole('button', { name: '建立專案' })).toBeVisible()
    await expect(page.getByRole('button', { name: '建立專案' })).toBeInViewport()
    await expect(page.getByText('尚未建立任何專案')).toBeVisible()
  })

  test('可以從專案列表開啟建立專案對話框', async ({ page }) => {
    await page.goto('/')

    await openCreateProjectModal(page)
  })

  test('專案列表載入失敗時顯示 page-level 錯誤而不是空白清單', async ({ page }) => {
    await page.route('**/api/projects', async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ message: 'load failed' }),
        })
        return
      }

      await route.continue()
    })

    await page.goto('/')

    await expect(page.getByText('無法載入專案列表，請確認後端 API 已啟動。')).toBeVisible()
    await expect(page.getByText('尚未建立任何專案')).toHaveCount(0)
  })

  // Create Project Modal
  test('建立專案對話框開啟後焦點直接落在專案名稱欄位，且只有單一輸入欄位', async ({ page }) => {
    await page.goto('/')
    await openCreateProjectModal(page)

    const dialog = page.getByRole('dialog', { name: '建立專案' })
    const projectNameInput = dialog.getByLabel('專案名稱')

    await expect(dialog.getByRole('textbox')).toHaveCount(1)
    await expect(projectNameInput).toBeFocused()
  })

  test('拒絕空白的專案名稱', async ({ page }) => {
    await page.goto('/')
    await openCreateProjectModal(page)

    const dialog = page.getByRole('dialog', { name: '建立專案' })

    await dialog.getByRole('button', { name: '建立', exact: true }).click()

    await expect(page.getByText('專案名稱為必填欄位')).toBeVisible()
    await expect(dialog).toBeVisible()
  })

  test('建立專案失敗時保持對話框開啟並保留輸入內容', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await page.route('**/api/projects', async (route) => {
      if (route.request().method() !== 'POST') {
        await route.continue()
        return
      }

      await new Promise((resolve) => setTimeout(resolve, 300))
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'create failed' }),
      })
    })

    await page.goto('/')
    await openCreateProjectModal(page)

    const dialog = page.getByRole('dialog', { name: '建立專案' })
    const projectNameInput = dialog.getByLabel('專案名稱')
    const submitButton = dialog.getByRole('button', { name: '建立', exact: true })
    const cancelButton = dialog.getByRole('button', { name: '取消' })

    await projectNameInput.fill(projectName)
    await submitButton.click()

    await expect(submitButton).toBeDisabled()
    await expect(cancelButton).toBeDisabled()
    await expect(page.getByText('建立專案失敗，請稍後再試。')).toBeVisible()
    await expect(dialog).toBeVisible()
    await expect(projectNameInput).toHaveValue(projectName)
  })

  // Project Kanban Board
  test('建立專案後看板首屏顯示目前專案與預設欄位', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)

    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expect(page.getByRole('button', { name: '建立任務' })).toBeInViewport()

    for (const column of workflowColumns) {
      await expect(page.getByTestId(`workflow-column-${column.key}`)).toContainText(column.label)
    }
  })

  test('專案看板的空欄位顯示預設訊息', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)

    for (const column of workflowColumns) {
      await expect(page.getByTestId(`workflow-column-${column.key}`)).toContainText('目前沒有任務')
    }
  })

  // Create Task Modal
  test('建立任務對話框開啟後焦點直接落在任務標題欄位，且只有單一輸入欄位', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)
    await openCreateTaskModal(page)

    const dialog = page.getByRole('dialog', { name: '建立任務' })
    const taskTitleInput = dialog.getByLabel('任務標題')

    await expect(dialog.getByRole('textbox')).toHaveCount(1)
    await expect(taskTitleInput).toBeFocused()
  })

  test('拒絕空白的任務標題', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)
    await openCreateTaskModal(page)

    const dialog = page.getByRole('dialog', { name: '建立任務' })

    await dialog.getByRole('button', { name: '建立', exact: true }).click()

    await expect(page.getByText('任務標題為必填欄位')).toBeVisible()
    await expect(dialog).toBeVisible()
  })

  test('建立任務失敗時保持對話框開啟並保留輸入內容', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await page.route('**/api/projects/*/tasks', async (route) => {
      if (route.request().method() !== 'POST') {
        await route.continue()
        return
      }

      await new Promise((resolve) => setTimeout(resolve, 300))
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'create failed' }),
      })
    })

    await setupProjectBoard(page, projectName)
    await openCreateTaskModal(page)

    const dialog = page.getByRole('dialog', { name: '建立任務' })
    const taskTitleInput = dialog.getByLabel('任務標題')
    const submitButton = dialog.getByRole('button', { name: '建立', exact: true })
    const cancelButton = dialog.getByRole('button', { name: '取消' })

    await taskTitleInput.fill(taskTitle)
    await submitButton.click()

    await expect(submitButton).toBeDisabled()
    await expect(cancelButton).toBeDisabled()
    await expect(page.getByText('建立任務失敗，請稍後再試。')).toBeVisible()
    await expect(dialog).toBeVisible()
    await expect(taskTitleInput).toHaveValue(taskTitle)
  })

  test('使用者可以建立任務並顯示在待處理欄位', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)

    const initialColumn = page.getByTestId('workflow-column-todo')
    await expect(initialColumn).toContainText(taskTitle)
    await expect(initialColumn).not.toContainText('目前沒有任務')
  })

  // Task Detail Drawer
  test('任務詳細資訊顯示基本資訊，未完成任務不顯示完成時間', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)

    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog).toBeVisible()
    await expect(detailDialog.getByRole('heading', { name: '任務詳細資訊' })).toBeVisible()
    await expect(detailDialog.getByRole('heading', { name: taskTitle })).toBeVisible()
    await expect(detailDialog.getByText('待處理', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已建立任務', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('完成時間', { exact: true })).toHaveCount(0)
  })

  test('重新整理後仍可看到已建立的專案與任務', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)
    await page.reload()

    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expect(page.getByTestId('workflow-column-todo')).toContainText(taskTitle)
  })

  // Board State Change
  test('任務卡片與 workflow 欄位提供穩定定位方式供狀態移動驗收', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)

    const taskCard = getTaskCard(page, 'todo', taskTitle)
    const activeColumn = page.getByTestId('workflow-column-active')

    await expect(taskCard).toBeVisible()
    await expect(activeColumn).toBeVisible()
  })

  test('使用者可以拖曳任務到進行中欄位，並在詳細資訊看到最新狀態', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)

    const taskCard = getTaskCard(page, 'todo', taskTitle)
    const activeColumn = page.getByTestId('workflow-column-active')

  await taskCard.dragTo(activeColumn)

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

    const project = await createProjectThroughApi(request, projectName)
    const task = await createTaskThroughApi(request, project.id, taskTitle)
    await moveTaskStateThroughApi(request, project.id, task.id, 'active')

    await page.goto('/')
    await page.getByRole('button', { name: projectName }).click()
    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()

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
})