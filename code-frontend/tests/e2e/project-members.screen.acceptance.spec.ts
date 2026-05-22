import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  openProjectMembersPanel,
  setupProjectBoard,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Project Members Screen', () => {
  test('Project Owner 開啟專案成員面板後可看到成員管理入口與邀請表單', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)
    await openProjectMembersPanel(page)

    await expect(page.getByRole('heading', { name: '專案成員' })).toBeVisible()
    await expect(page.getByLabel('邀請對象')).toBeVisible()
    await expect(page.getByRole('button', { name: '邀請', exact: true })).toBeVisible()
  })

  test('專案成員面板會顯示目前專案成員與待處理邀請資訊', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)
    await openProjectMembersPanel(page)

    await expect(page.locator('.collaboration-role-badge').filter({ hasText: '專案擁有者' })).toBeVisible()
    await expect(page.getByText('待處理邀請', { exact: true })).toBeVisible()
  })
})