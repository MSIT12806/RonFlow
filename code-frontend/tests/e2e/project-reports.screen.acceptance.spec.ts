import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  openProjectReportsView,
  setupProjectBoard,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Project Reports Screen', () => {
  test('使用者可以從 Project Board 開啟專案報表頁並看到工作流量報表入口', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)
    await openProjectReportsView(page)

    await expect(page.getByText(`${projectName} 的統計摘要`)).toBeVisible()
    await expect(page.getByRole('button', { name: '工作流量', exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: '任務停留', exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: '週期時間', exact: true })).toBeVisible()
    await expect(page.getByLabel('統計粒度')).toHaveValue('day')
    await expect(page.getByText('目前尚無報表資料。')).toBeVisible()

    await page.getByRole('button', { name: '回到看板', exact: true }).click()
    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
  })
})