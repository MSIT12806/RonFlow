import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  getTaskCard,
  setupTaskBoard,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Task Workflow Screen', () => {
  test('任務卡片與 workflow 欄位提供穩定定位方式供狀態移動驗收', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)

    const taskCard = getTaskCard(page, 'todo', taskTitle)
    const activeColumn = page.getByTestId('workflow-column-active')

    await expect(taskCard).toBeVisible()
    await expect(activeColumn).toBeVisible()
  })
})
