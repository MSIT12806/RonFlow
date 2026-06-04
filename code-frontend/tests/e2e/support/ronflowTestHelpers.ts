import { expect, type Page, type TestInfo } from '@playwright/test'
import { registerAndEnterWorkspace } from './ronflowAuthTestHelpers'

export const workflowColumns = [
  { key: 'todo', label: '待處理' },
  { key: 'active', label: '進行中' },
  { key: 'review', label: '審查中' },
  { key: 'done', label: '已完成' },
] as const

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

export async function openProjectMembersPanel(page: Page) {
  await page.getByText('專案成員', { exact: true }).click()
  await expect(page.getByRole('heading', { name: '專案成員' })).toBeVisible()
}

export async function openProjectReportsView(page: Page) {
  await page.getByRole('button', { name: '報表', exact: true }).click()
  await expect(page.getByRole('heading', { name: '專案報表' })).toBeVisible()
}

export async function openInvitationInbox(page: Page) {
  await page.getByText('邀請收件匣', { exact: true }).click()
  await expect(page.getByRole('heading', { name: '邀請收件匣' })).toBeVisible()
}

export async function openProjectFromList(page: Page, projectName: string) {
  await page.getByRole('button', { name: new RegExp(projectName) }).click()
  await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
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
  const actionsButton = detailDialog.getByRole('button', { name: /更多|更多操作/ })

  for (let attempt = 0; attempt < 2; attempt += 1) {
    await actionsButton.click()

    try {
      await expect(actionsButton).toHaveAttribute('aria-expanded', 'true')
      return
    } catch (error) {
      if (attempt === 1) {
        throw error
      }
    }
  }
}

async function clickTaskActionMenuItem(page: Page, actionName: string) {
  const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

  for (let attempt = 0; attempt < 2; attempt += 1) {
    try {
      await openTaskActions(page)

      const actionButton = detailDialog.getByRole('menuitem', { name: actionName, exact: true })
      await expect(actionButton).toBeVisible()
      await actionButton.click()
      return
    } catch (error) {
      if (attempt === 1) {
        throw error
      }
    }
  }
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
  await clickTaskActionMenuItem(page, '封存')
}

export async function moveTaskToTrashFromDrawer(page: Page) {
  await clickTaskActionMenuItem(page, '移到垃圾桶')
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
