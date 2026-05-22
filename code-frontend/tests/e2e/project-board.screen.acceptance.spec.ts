import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  openCreateTaskModal,
  openProjectMembersPanel,
  setupProjectBoard,
  workflowColumns,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Project Board Screen', () => {
  test('建立專案後看板首屏顯示目前專案與預設欄位', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)

    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expect(page.getByRole('button', { name: '建立任務' })).toBeInViewport()
    await expect(page.getByText('專案成員', { exact: true })).toBeVisible()

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

  test('Project Owner 可以從看板開啟專案成員面板', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)
    await openProjectMembersPanel(page)
  })
})
