import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  openTaskDetail,
  setupTaskBoard,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Task Reminders', () => {
  test('使用者可以在 Task Detail Drawer 新增一筆提醒', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const reminderDateTime = '2026-05-20T09:00'
    const reminderDescription = '提醒確認欄位狀態'

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog.getByText('提醒', { exact: true })).toBeVisible()
    await detailDialog.getByLabel('提醒時間').fill(reminderDateTime)
    await detailDialog.getByLabel('提醒說明').fill(reminderDescription)
    await detailDialog.getByRole('button', { name: '新增提醒', exact: true }).click()

    await expect(detailDialog).toContainText('2026-05-20 09:00')
    await expect(detailDialog).toContainText(reminderDescription)
  })
})
