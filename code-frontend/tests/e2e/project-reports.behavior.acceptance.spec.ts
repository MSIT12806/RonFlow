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
})