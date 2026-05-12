import { expect, test, type APIRequestContext, type Page, type TestInfo } from '@playwright/test'

const workflowColumns = [
  { key: 'todo', label: '待處理' },
  { key: 'active', label: '進行中' },
  { key: 'review', label: '審查中' },
  { key: 'done', label: '已完成' },
] as const
const backendApiBaseUrl = process.env.RONFLOW_E2E_BACKEND_API_BASE_URL ?? 'http://127.0.0.1:5079/api'

type ProjectResponse = {
  id: string
}

type TaskResponse = {
  id: string
}

function createTaskTitle(testInfo: TestInfo, label: string) {
  return `${label} ${testInfo.workerIndex}-${testInfo.retry}-${Date.now()}`
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

async function expectTaskOrder(page: Page, stateKey: string, expectedTaskTitles: string[]) {
  await expect(
    page.getByTestId(`workflow-column-${stateKey}`).locator('.task-title'),
  ).toHaveText(expectedTaskTitles)
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
    const closeButton = dialog.getByRole('button', { name: '關閉視窗' })

    await projectNameInput.fill(projectName)
    await submitButton.click()

    await expect(projectNameInput).toBeDisabled()
    await expect(submitButton).toBeDisabled()
    await expect(cancelButton).toBeDisabled()
    await expect(closeButton).toBeDisabled()
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
    const closeButton = dialog.getByRole('button', { name: '關閉視窗' })

    await taskTitleInput.fill(taskTitle)
    await submitButton.click()

    await expect(taskTitleInput).toBeDisabled()
    await expect(submitButton).toBeDisabled()
    await expect(cancelButton).toBeDisabled()
    await expect(closeButton).toBeDisabled()
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

  test('任務詳細資訊載入中時在 drawer 內顯示 shared loading state', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await page.route('**/api/projects/*/tasks/*', async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await new Promise((resolve) => setTimeout(resolve, 300))
      await route.continue()
    })

    await setupTaskBoard(page, projectName, taskTitle)
    await page.getByTestId('workflow-column-todo').getByText(taskTitle, { exact: true }).click()

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog).toBeVisible()
    await expect(detailDialog.getByTestId('base-loading-state')).toContainText('正在載入任務詳細資訊...')
    await expect(detailDialog.getByRole('heading', { name: taskTitle })).toBeVisible()
  })

  test('任務詳細資訊載入失敗時在 drawer 內顯示 shared error state', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await page.route('**/api/projects/*/tasks/*', async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'detail failed' }),
      })
    })

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog).toBeVisible()
    await expect(detailDialog.getByTestId('base-error-state')).toContainText('無法載入任務詳細資訊，請重新整理後再試。')
    await expect(page.getByText('無法載入任務詳細資訊，請重新整理後再試。')).toHaveCount(1)
  })

  test('重新整理後仍可看到已建立的專案與任務', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)

    await page.evaluate(() => {
      window.localStorage.clear()
      window.sessionStorage.clear()
    })
    await page.context().clearCookies()
    await page.reload()

    await page.getByRole('button', { name: projectName }).click()
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

  test('狀態變更 API 失敗時，Task Card 會回到原欄位並顯示錯誤訊息', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await page.route('**/api/projects/*/tasks/*/state', async (route) => {
      if (route.request().method() !== 'PATCH') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'move failed' }),
      })
    })

    await setupTaskBoard(page, projectName, taskTitle)

    await getTaskCard(page, 'todo', taskTitle).dragTo(page.getByTestId('workflow-column-active'))

    await expect(page.getByTestId('workflow-column-todo')).toContainText(taskTitle)
    await expect(page.getByTestId('workflow-column-active')).not.toContainText(taskTitle)
    await expect(page.getByTestId('base-error-state')).toContainText('變更任務狀態失敗，請稍後再試。')
  })

  test('使用者可以在 Drawer 修改任務標題、描述與到期日，並看到最新資料與活動紀錄', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const updatedTaskTitle = createTaskTitle(testInfo, 'Updated Task Title')
    const updatedDescription = '支援在 Drawer 直接編輯任務內容與活動紀錄'
    const updatedDueDate = '2026-05-20'

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await detailDialog.getByLabel('任務標題').fill(updatedTaskTitle)
    await detailDialog.getByLabel('任務描述').fill(updatedDescription)
    await detailDialog.getByLabel('到期日').fill(updatedDueDate)
    await detailDialog.getByRole('button', { name: '儲存變更' }).click()

    await expect(detailDialog.getByLabel('任務標題')).toHaveValue(updatedTaskTitle)
    await expect(detailDialog.getByLabel('任務描述')).toHaveValue(updatedDescription)
    await expect(detailDialog.getByLabel('到期日')).toHaveValue(updatedDueDate)
    await expect(detailDialog.getByText('已更新任務標題', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已更新任務描述', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已設定到期日為 2026/05/20', { exact: true })).toBeVisible()
    await expect(page.getByTestId('workflow-column-todo')).toContainText(updatedTaskTitle)
    await expect(page.getByTestId('workflow-column-todo')).not.toContainText(taskTitle)
  })

  test('使用者可以將已完成任務重新開啟，清除目前完成時間並記錄重新開啟活動', async ({ page, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    const project = await createProjectThroughApi(request, projectName)
    const task = await createTaskThroughApi(request, project.id, taskTitle)
    await moveTaskStateThroughApi(request, project.id, task.id, 'active')
    await moveTaskStateThroughApi(request, project.id, task.id, 'done')

    await page.goto('/')
    await page.getByRole('button', { name: projectName }).click()
    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()

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