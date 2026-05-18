import { expect, test, type Locator } from '@playwright/test'
import {
  createScenarioData,
  openTaskDetail,
  setupTaskBoard,
} from './support/ronflowTestHelpers'

function getRemindersSection(detailDialog: Locator) {
  return detailDialog.getByTestId('task-reminders-section')
}

test.describe('RonFlow UI/UX 驗收規格 - Task Reminders Screen', () => {
  test('提醒區塊顯示建立提醒所需欄位與操作', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })
    const remindersSection = getRemindersSection(detailDialog)

    await expect(remindersSection).toContainText('提醒')
    await expect(detailDialog.getByLabel('提醒時間')).toBeVisible()
    await expect(detailDialog.getByLabel('提醒說明')).toBeVisible()
    await expect(detailDialog.getByRole('button', { name: '新增提醒', exact: true })).toBeVisible()
  })

  test('當裝置沒有通知權限或沒有 push subscription 時，畫面明確告知提醒可能無法送達', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await page.addInitScript(() => {
      Object.defineProperty(window.Notification, 'permission', {
        configurable: true,
        get: () => 'denied',
      })
    })

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })
    const deliveryStatus = getRemindersSection(detailDialog).getByTestId('reminder-delivery-status')

    await expect(deliveryStatus).toContainText(/提醒可能無法送達/)
  })
})
