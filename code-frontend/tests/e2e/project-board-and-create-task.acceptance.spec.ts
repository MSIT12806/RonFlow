import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  openCreateTaskModal,
  setupProjectBoard,
  setupTaskBoard,
  workflowColumns,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Project Board 與 Create Task', () => {
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
})
