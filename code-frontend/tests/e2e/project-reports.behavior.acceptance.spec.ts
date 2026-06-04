import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  dragTaskToColumn,
  openProjectReportsView,
  setupTaskBoard,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Project Reports Behavior', () => {
  test('使用者完成 workflow 流轉後，可以在工作流量報表看到對應統計', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)
    await dragTaskToColumn(page, 'todo', 'active', taskTitle)
    await dragTaskToColumn(page, 'active', 'review', taskTitle)
    await dragTaskToColumn(page, 'review', 'done', taskTitle)

    await openProjectReportsView(page)

    const bucket = page.getByTestId('workflow-throughput-bucket').first()
    await expect(bucket).toBeVisible({ timeout: 10000 })
    await expect(bucket.getByTestId('throughput-created-count')).toHaveText('1')
    await expect(bucket.getByTestId('throughput-active-count')).toHaveText('1')
    await expect(bucket.getByTestId('throughput-review-count')).toHaveText('1')
    await expect(bucket.getByTestId('throughput-completed-count')).toHaveText('1')
    await expect(bucket.getByTestId('throughput-reopened-count')).toHaveText('0')
  })

  test('使用者可以在任務停留報表調整閾值並直接開啟 Task Detail Drawer', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)
    await openProjectReportsView(page)

    await page.getByRole('button', { name: '任務停留', exact: true }).click()
    await page.getByLabel('Todo 閾值（天）').fill('0')

    const agingItem = page.getByTestId('task-aging-item').filter({ hasText: taskTitle }).first()
    await expect(agingItem).toBeVisible()
    await expect(agingItem).toContainText('待處理')

    await agingItem.click()
    await expect(page.getByRole('dialog', { name: '任務詳細資訊' })).toBeVisible()
    await expect(page.getByRole('dialog', { name: '任務詳細資訊' })).toContainText(taskTitle)
  })
})