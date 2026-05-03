import { expect, test, type Page, type TestInfo } from '@playwright/test'

const workflowColumns = [
  { key: 'todo', label: '待處理' },
  { key: 'active', label: '進行中' },
  { key: 'review', label: '審查中' },
  { key: 'done', label: '已完成' },
] as const

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
  test('專案列表首屏顯示建立專案入口與空狀態', async ({ page }) => {
    await page.goto('/')

    await expect(page.getByRole('heading', { name: '專案列表' })).toBeVisible()
    await expect(page.getByRole('button', { name: '建立專案' })).toBeVisible()
    await expect(page.getByRole('button', { name: '建立專案' })).toBeInViewport()
    await expect(page.getByText('尚未建立任何專案')).toBeVisible()
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

  test('建立專案對話框開啟後焦點直接落在專案名稱欄位，且只有單一輸入欄位', async ({ page }) => {
    await page.goto('/')
    await openCreateProjectModal(page)

    const dialog = page.getByRole('dialog', { name: '建立專案' })
    const projectNameInput = dialog.getByLabel('專案名稱')

    await expect(dialog.getByRole('textbox')).toHaveCount(1)
    await expect(projectNameInput).toBeFocused()
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

  test('建立專案後看板首屏顯示目前專案與預設欄位', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)

    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expect(page.getByRole('button', { name: '建立任務' })).toBeInViewport()

    for (const column of workflowColumns) {
      await expect(page.getByTestId(`workflow-column-${column.key}`)).toContainText(column.label)
    }
  })

  test('建立任務對話框開啟後焦點直接落在任務標題欄位，且只有單一輸入欄位', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)
    await openCreateTaskModal(page)

    const dialog = page.getByRole('dialog', { name: '建立任務' })
    const taskTitleInput = dialog.getByLabel('任務標題')

    await expect(dialog.getByRole('textbox')).toHaveCount(1)
    await expect(taskTitleInput).toBeFocused()
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

  test('任務詳細資訊顯示基本資訊，未完成任務不顯示完成時間', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)

    await page.getByTestId('workflow-column-todo').getByText(taskTitle, { exact: true }).click()

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog).toBeVisible()
    await expect(detailDialog.getByRole('heading', { name: '任務詳細資訊' })).toBeVisible()
    await expect(detailDialog.getByRole('heading', { name: taskTitle })).toBeVisible()
    await expect(detailDialog.getByText('待處理', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已建立任務', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('完成時間', { exact: true })).toHaveCount(0)
  })

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
})