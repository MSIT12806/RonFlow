import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  openCreateTaskModal,
  setupProjectBoard,
  setupTaskBoard,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Project Board Behavior', () => {
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
