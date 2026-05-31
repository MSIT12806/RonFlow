import { expect, test, type Locator } from '@playwright/test'
import {
  createScenarioData,
  openTaskDetail,
  setupTaskBoard,
} from './support/ronflowTestHelpers'

function getTraceabilitySection(detailDialog: Locator) {
  return detailDialog.getByTestId('task-code-traceability-section')
}

function getTraceabilityCategory(detailDialog: Locator, category: 'api' | 'frontend-pages' | 'frontend-components') {
  return getTraceabilitySection(detailDialog).getByTestId(`task-code-traceability-${category}`)
}

async function addTraceabilityItem(categorySection: Locator, changeType: 'added' | 'modified' | 'removed', target: string) {
  await categorySection.getByRole('button', { name: '新增紀錄', exact: true }).click()

  const item = categorySection.getByTestId('task-code-traceability-item').last()
  await item.getByLabel('變更類型').selectOption(changeType)
  await item.getByLabel('項目名稱').fill(target)
}

test.describe('RonFlow UI/UX 驗收規格 - Project Code Traceability Query', () => {
  test('使用者可以從 Project Board 查詢跨 Task 的程式修改紀錄', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })
    await detailDialog.getByRole('button', { name: '編輯', exact: true }).click()

    await addTraceabilityItem(getTraceabilityCategory(detailDialog, 'api'), 'added', 'GET /api/projects/{projectId}/code-traceability')
    await addTraceabilityItem(getTraceabilityCategory(detailDialog, 'frontend-pages'), 'modified', 'Project Board')
    await addTraceabilityItem(getTraceabilityCategory(detailDialog, 'frontend-components'), 'added', 'CodeTraceabilityQueryView')

    await detailDialog.getByRole('button', { name: '儲存變更', exact: true }).click()
    await expect(getTraceabilityCategory(detailDialog, 'frontend-components')).toContainText('CodeTraceabilityQueryView')
    await detailDialog.getByRole('button', { name: '關閉視窗', exact: true }).click()

    await page.getByRole('button', { name: '程式修改紀錄', exact: true }).click()

    await expect(page.getByRole('heading', { name: '程式修改紀錄' })).toBeVisible()
    await expect(page.getByTestId('code-traceability-result')).toHaveCount(3)
    await expect(page.getByText(taskTitle)).toBeVisible()
    await expect(page.getByText('GET /api/projects/{projectId}/code-traceability')).toBeVisible()
    await expect(page.getByText('Project Board')).toBeVisible()
    await expect(page.getByText('CodeTraceabilityQueryView')).toBeVisible()

    await page.getByLabel('紀錄類別').selectOption('frontendComponents')
    await expect(page.getByTestId('code-traceability-result')).toHaveCount(1)
    await expect(page.getByText('CodeTraceabilityQueryView')).toBeVisible()
    await expect(page.getByText('GET /api/projects/{projectId}/code-traceability')).not.toBeVisible()

    await page.getByLabel('關鍵字').fill('query')
    await expect(page.getByTestId('code-traceability-result')).toHaveCount(1)

    await page.getByRole('button', { name: '返回看板', exact: true }).click()
    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
  })
})