import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  dragTaskToColumn,
  openProjectReportsView,
  openProjectFromList,
  setupTaskBoard,
} from './support/ronflowTestHelpers'
import {
  createProjectThroughApi,
  createTaskThroughApi,
  moveTaskStateThroughApi,
  registerRonFlowApiUser,
} from './support/ronflowApiTestHelpers'
import { createRonFlowAuthUser, loginAndEnterWorkspace } from './support/ronflowAuthTestHelpers'

async function setupCompletedTaskBoardThroughApi(
  request: Parameters<typeof test>[0]['request'],
  page: Parameters<typeof test>[0]['page'],
  projectName: string,
  taskTitle: string,
) {
  const userSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('cycle-report-owner'))
  const project = await createProjectThroughApi(request, userSession, projectName)
  const task = await createTaskThroughApi(request, userSession, project.id, taskTitle)

  await moveTaskStateThroughApi(request, userSession, project.id, task.id, 'active')
  await moveTaskStateThroughApi(request, userSession, project.id, task.id, 'done')

  await loginAndEnterWorkspace(page, userSession.user)
  await openProjectFromList(page, projectName)
}

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

  test('使用者可以在週期時間報表查看 lead time 與 cycle time 指標', async ({ page, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupCompletedTaskBoardThroughApi(request, page, projectName, taskTitle)
    await openProjectReportsView(page)

    await page.getByRole('button', { name: '週期時間', exact: true }).click()

    await expect(page.getByTestId('cycle-time-lead-time-card')).toContainText('樣本數 1')
    await expect(page.getByTestId('cycle-time-lead-time-card')).toContainText('平均值')
    await expect(page.getByTestId('cycle-time-cycle-time-card')).toContainText('樣本數 1')
    await expect(page.getByTestId('cycle-time-cycle-time-card')).toContainText('p90')
  })
})