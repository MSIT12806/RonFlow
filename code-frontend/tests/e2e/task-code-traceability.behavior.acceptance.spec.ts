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

async function enterTaskDetailEditMode(detailDialog: Locator) {
  await detailDialog.getByRole('button', { name: '編輯', exact: true }).click()
  await expect(detailDialog.getByRole('button', { name: '儲存變更', exact: true })).toBeVisible()
}

async function addTraceabilityItem(categorySection: Locator, changeType: 'added' | 'modified' | 'removed', target: string) {
  await categorySection.getByRole('button', { name: '新增紀錄', exact: true }).click()

  const item = categorySection.getByTestId('task-code-traceability-item').last()
  await item.getByLabel('變更類型').selectOption(changeType)
  await item.getByLabel('項目名稱').fill(target)
}

test.describe('RonFlow UI/UX 驗收規格 - Task Code Traceability Behavior', () => {
  test('使用者可以在 Task Detail Drawer 記錄並重新讀出結構化程式修改追蹤', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })
    await enterTaskDetailEditMode(detailDialog)

    await addTraceabilityItem(getTraceabilityCategory(detailDialog, 'api'), 'added', 'GET /api/build-info')
    await addTraceabilityItem(getTraceabilityCategory(detailDialog, 'frontend-pages'), 'modified', 'ProjectBoardPage')
    await addTraceabilityItem(getTraceabilityCategory(detailDialog, 'frontend-components'), 'removed', 'LegacyTaskDrawer')

    await detailDialog.getByRole('button', { name: '儲存變更', exact: true }).click()

    await expect(getTraceabilityCategory(detailDialog, 'api')).toContainText('新增')
    await expect(getTraceabilityCategory(detailDialog, 'api')).toContainText('GET /api/build-info')
    await expect(getTraceabilityCategory(detailDialog, 'frontend-pages')).toContainText('修改')
    await expect(getTraceabilityCategory(detailDialog, 'frontend-pages')).toContainText('ProjectBoardPage')
    await expect(getTraceabilityCategory(detailDialog, 'frontend-components')).toContainText('移除')
    await expect(getTraceabilityCategory(detailDialog, 'frontend-components')).toContainText('LegacyTaskDrawer')

    await detailDialog.getByRole('button', { name: '關閉視窗', exact: true }).click()
    await openTaskDetail(page, 'todo', taskTitle)

    await expect(getTraceabilityCategory(detailDialog, 'api')).toContainText('GET /api/build-info')
    await expect(getTraceabilityCategory(detailDialog, 'frontend-pages')).toContainText('ProjectBoardPage')
    await expect(getTraceabilityCategory(detailDialog, 'frontend-components')).toContainText('LegacyTaskDrawer')
  })
})