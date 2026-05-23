import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  openProjectFromList,
  openTaskDetail,
  setupTaskBoard,
} from './support/ronflowTestHelpers'
import {
  configureTestFaultsThroughApi,
  createProjectThroughApi,
  createTaskThroughApi,
  registerRonFlowApiUser,
} from './support/ronflowApiTestHelpers'
import { createRonFlowAuthUser, loginAndEnterWorkspace } from './support/ronflowAuthTestHelpers'

async function setupTaskBoardThroughApi(
  request: Parameters<typeof test>[0]['request'],
  page: Parameters<typeof test>[0]['page'],
  projectName: string,
  taskTitle: string,
) {
  const userSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))
  const project = await createProjectThroughApi(request, userSession, projectName)
  await createTaskThroughApi(request, userSession, project.id, taskTitle)

  await loginAndEnterWorkspace(page, userSession.user)
  await openProjectFromList(page, projectName)

  return { userSession }
}

test.describe('RonFlow UI/UX 驗收規格 - Task Detail Screen', () => {
  test('任務詳細資訊顯示基本資訊，未完成任務不顯示完成時間', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog).toBeVisible()
    await expect(detailDialog.getByRole('heading', { name: '任務詳細資訊' })).toBeVisible()
    await expect(detailDialog.getByLabel('任務標題')).toHaveValue(taskTitle)
    await expect(detailDialog.getByText('待處理', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已建立任務', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('完成時間', { exact: true })).toHaveCount(0)
  })

  test('任務詳細資訊載入中時在 drawer 內顯示 shared loading state', async ({ page, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    const { userSession } = await setupTaskBoardThroughApi(request, page, projectName, taskTitle)

    await configureTestFaultsThroughApi(request, userSession, [{
      method: 'GET',
      pathPattern: '/api/projects/*/tasks/*',
      statusCode: null,
      delayMs: 600,
    }])

    await page.getByTestId('workflow-column-todo').getByText(taskTitle, { exact: true }).click()

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog).toBeVisible()
    await expect(detailDialog.getByTestId('base-loading-state')).toContainText('正在載入任務詳細資訊...')
    await expect(detailDialog.getByRole('heading', { name: taskTitle })).toBeVisible()
  })

  test('任務詳細資訊載入失敗時在 drawer 內顯示 shared error state', async ({ page, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    const { userSession } = await setupTaskBoardThroughApi(request, page, projectName, taskTitle)

    await configureTestFaultsThroughApi(request, userSession, [{
      method: 'GET',
      pathPattern: '/api/projects/*/tasks/*',
      statusCode: 500,
      message: 'detail failed',
    }])

    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog).toBeVisible()
    await expect(detailDialog.getByTestId('base-error-state')).toContainText('無法載入任務詳細資訊，請重新整理後再試。')
    await expect(page.getByText('無法載入任務詳細資訊，請重新整理後再試。')).toHaveCount(1)
  })
})
