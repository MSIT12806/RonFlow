import { expect, type APIRequestContext, type Page, type TestInfo } from '@playwright/test'
import { registerAndEnterWorkspace } from './ronflowAuthTestHelpers'

export const workflowColumns = [
  { key: 'todo', label: '待處理' },
  { key: 'active', label: '進行中' },
  { key: 'review', label: '審查中' },
  { key: 'done', label: '已完成' },
] as const

type ProjectResponse = {
  id: string
}

type TaskResponse = {
  id: string
}

type ProcessEnvironment = typeof globalThis & {
  process?: {
    env?: Record<string, string | undefined>
  }
}

const backendApiBaseUrl = (globalThis as ProcessEnvironment).process?.env?.RONFLOW_E2E_BACKEND_API_BASE_URL
  ?? 'http://127.0.0.1:5079/api'

export function createTaskTitle(testInfo: TestInfo, label: string) {
  return `${label} ${testInfo.workerIndex}-${testInfo.retry}-${Date.now()}`
}

export function createScenarioData(testInfo: TestInfo) {
  const suffix = `${testInfo.workerIndex}-${testInfo.retry}-${Date.now()}`

  return {
    projectName: `RonFlow Project ${suffix}`,
    taskTitle: `Build Kanban Board ${suffix}`,
  }
}

export async function openCreateProjectModal(page: Page) {
  await page.getByRole('button', { name: '建立專案' }).click()
  await expect(page.getByRole('dialog', { name: '建立專案' })).toBeVisible()
}

export async function createProject(page: Page, projectName: string) {
  const dialog = page.getByRole('dialog', { name: '建立專案' })

  await dialog.getByLabel('專案名稱').fill(projectName)
  await dialog.getByRole('button', { name: '建立', exact: true }).click()

  await expect(dialog).not.toBeVisible()
  await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
}

export async function openCreateTaskModal(page: Page) {
  await page.getByRole('button', { name: '建立任務' }).click()
  await expect(page.getByRole('dialog', { name: '建立任務' })).toBeVisible()
}

export async function createTask(page: Page, taskTitle: string) {
  const dialog = page.getByRole('dialog', { name: '建立任務' })

  await dialog.getByLabel('任務標題').fill(taskTitle)
  await dialog.getByRole('button', { name: '建立', exact: true }).click()

  await expect(dialog).not.toBeVisible()
}

export async function openTaskDetail(page: Page, stateKey: string, taskTitle: string) {
  await page.getByTestId(`workflow-column-${stateKey}`).getByText(taskTitle, { exact: true }).click()
  await expect(page.getByRole('dialog', { name: '任務詳細資訊' })).toBeVisible()
}

export async function openTaskActions(page: Page) {
  const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

  await detailDialog.getByRole('button', { name: /更多|更多操作/ }).click()
}

export async function openArchivedTasksView(page: Page) {
  await page.getByRole('button', { name: '已封存任務' }).click()
  await expect(page.getByRole('heading', { name: '已封存任務' })).toBeVisible()
}

export async function openTrashView(page: Page) {
  await page.getByRole('button', { name: '垃圾桶' }).click()
  await expect(page.getByRole('heading', { name: '垃圾桶' })).toBeVisible()
}

export function getLifecycleTaskItem(page: Page, taskTitle: string) {
  return page.getByTestId('lifecycle-task-item').filter({ hasText: taskTitle }).first()
}

export async function archiveTaskFromDrawer(page: Page) {
  await openTaskActions(page)
  await page.getByRole('menuitem', { name: '封存' }).click()
}

export async function moveTaskToTrashFromDrawer(page: Page) {
  await openTaskActions(page)
  await page.getByRole('menuitem', { name: '移到垃圾桶' }).click()
}

export function getTaskCard(page: Page, stateKey: string, taskTitle: string) {
  return page
    .getByTestId(`workflow-column-${stateKey}`)
    .locator('.task-card-main')
    .filter({ hasText: taskTitle })
    .first()
}

export async function dragTaskToColumn(page: Page, fromStateKey: string, toStateKey: string, taskTitle: string) {
  const taskCard = getTaskCard(page, fromStateKey, taskTitle)
  const sourceColumn = page.getByTestId(`workflow-column-${fromStateKey}`)
  const targetColumn = page.getByTestId(`workflow-column-${toStateKey}`)

  for (let attempt = 0; attempt < 2; attempt += 1) {
    const responsePromise = page.waitForResponse((response) => {
      return response.request().method() === 'PATCH'
        && /\/api\/projects\/[^/]+\/tasks\/[^/]+\/state$/.test(response.url())
        && response.ok()
    }, { timeout: 3000 }).catch(() => null)

    await taskCard.dragTo(targetColumn)

    const response = await responsePromise
    if (response) {
      break
    }
  }

  await expect(targetColumn).toContainText(taskTitle, { timeout: 10000 })
  await expect(sourceColumn).not.toContainText(taskTitle, { timeout: 10000 })
}

export async function createProjectThroughApi(request: APIRequestContext, projectName: string) {
  const response = await request.post(`${backendApiBaseUrl}/projects`, {
    data: { name: projectName },
  })

  expect(response.ok()).toBeTruthy()
  return response.json() as Promise<ProjectResponse>
}

export async function createTaskThroughApi(request: APIRequestContext, projectId: string, taskTitle: string) {
  const response = await request.post(`${backendApiBaseUrl}/projects/${projectId}/tasks`, {
    data: { title: taskTitle },
  })

  expect(response.ok()).toBeTruthy()
  return response.json() as Promise<TaskResponse>
}

export async function moveTaskStateThroughApi(
  request: APIRequestContext,
  projectId: string,
  taskId: string,
  stateKey: string,
) {
  const response = await request.patch(`${backendApiBaseUrl}/projects/${projectId}/tasks/${taskId}/state`, {
    data: { stateKey },
  })

  expect(response.ok()).toBeTruthy()
}

export async function setupProjectBoard(page: Page, projectName: string) {
  await registerAndEnterWorkspace(page)
  await openCreateProjectModal(page)
  await createProject(page, projectName)
}

export async function setupTaskBoard(page: Page, projectName: string, taskTitle: string) {
  await setupProjectBoard(page, projectName)
  await openCreateTaskModal(page)
  await createTask(page, taskTitle)
}

export async function expectTaskOrder(page: Page, stateKey: string, expectedTaskTitles: string[]) {
  await expect(
    page.getByTestId(`workflow-column-${stateKey}`).locator('.task-title'),
  ).toHaveText(expectedTaskTitles)
}
