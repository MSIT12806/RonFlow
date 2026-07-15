import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  getTaskCard,
} from './support/ronflowTestHelpers'
import { createRonFlowAuthUser, loginAndEnterWorkspace } from './support/ronflowAuthTestHelpers'
import {
  configureTestFaultsThroughApi,
  createProjectThroughApi,
  createTaskThroughApi,
  moveTaskStateThroughApi,
  registerRonFlowApiUser,
  replaceProjectSubtaskTemplatesThroughApi,
} from './support/ronflowApiTestHelpers'

test.describe('Task state refresh behavior', () => {
  test('狀態變更後重新整理看板時保留目前內容，不會閃成載入畫面', async ({ page, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const userSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))
    const project = await createProjectThroughApi(request, userSession, projectName)
    await replaceProjectSubtaskTemplatesThroughApi(request, userSession, project.id, ['完成 short 任務'])
    await createTaskThroughApi(request, userSession, project.id, taskTitle, true)
    await loginAndEnterWorkspace(page, userSession.user)

    await expect(getTaskCard(page, 'todo', taskTitle)).toBeVisible()
    await configureTestFaultsThroughApi(request, userSession, [{
      method: 'GET',
      pathPattern: '/api/projects/*/board',
      delayMs: 1200,
    }])

    let stateChanged = false
    for (let attempt = 0; attempt < 2 && !stateChanged; attempt += 1) {
      const response = page.waitForResponse((candidate) =>
        candidate.request().method() === 'PATCH'
        && /\/api\/projects\/[^/]+\/tasks\/[^/]+\/state$/.test(candidate.url())
        && candidate.ok(), { timeout: 3000 }).catch(() => null)

      await getTaskCard(page, 'todo', taskTitle).dragTo(page.getByTestId('workflow-column-active'))
      stateChanged = await response !== null
    }

    expect(stateChanged).toBeTruthy()

    await expect(page.getByTestId('workflow-column-todo')).toContainText(taskTitle)
    await expect(page.getByText('正在載入專案看板...')).toHaveCount(0)
    await expect(page.getByTestId('workflow-column-active')).toContainText(taskTitle, { timeout: 5000 })
  })
})
