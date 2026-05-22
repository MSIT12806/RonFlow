import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  createTaskTitle,
  openTaskDetail,
  setupTaskBoard,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Task Detail Behavior', () => {
  test('重新整理後可透過 session restore 還原登入並看到已建立的專案與任務', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)

    await page.evaluate(() => {
      window.localStorage.clear()
      window.sessionStorage.clear()
    })
    await page.reload()

    await expect(page.getByRole('button', { name: '登出' })).toBeVisible()
    await page.getByRole('button', { name: projectName }).click()
    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expect(page.getByTestId('workflow-column-todo')).toContainText(taskTitle)
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
})
