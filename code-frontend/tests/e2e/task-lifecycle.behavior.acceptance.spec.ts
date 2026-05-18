import { expect, test } from '@playwright/test'
import {
  archiveTaskFromDrawer,
  createScenarioData,
  createTask,
  createTaskTitle,
  expectTaskOrder,
  getLifecycleTaskItem,
  moveTaskToTrashFromDrawer,
  openArchivedTasksView,
  openCreateTaskModal,
  openTaskDetail,
  openTrashView,
  setupProjectBoard,
  setupTaskBoard,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Task Lifecycle Behavior', () => {
  test('使用者可以封存任務，任務會離開看板並出現在已封存任務頁', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    await archiveTaskFromDrawer(page)

    await expect(page.getByRole('dialog', { name: '任務詳細資訊' })).toHaveCount(0)
    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expect(page.getByTestId('workflow-column-todo')).not.toContainText(taskTitle)

    await openArchivedTasksView(page)

    const archivedTaskItem = getLifecycleTaskItem(page, taskTitle)

    await expect(archivedTaskItem.getByText(taskTitle, { exact: true })).toBeVisible()
    await expect(archivedTaskItem.getByTestId('lifecycle-task-project-name')).toHaveText(projectName)
    await expect(archivedTaskItem.getByTestId('lifecycle-task-original-state')).toHaveText('待處理')
  })

  test('使用者可以從已封存任務頁還原任務回原欄位最後', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const firstTaskTitle = createTaskTitle(testInfo, 'Backlog Task A')
    const archivedTaskTitle = createTaskTitle(testInfo, 'Archived Task')

    await setupProjectBoard(page, projectName)

    await openCreateTaskModal(page)
    await createTask(page, firstTaskTitle)

    await openCreateTaskModal(page)
    await createTask(page, archivedTaskTitle)

    await openTaskDetail(page, 'todo', archivedTaskTitle)
    await archiveTaskFromDrawer(page)

    await openArchivedTasksView(page)
    await page.getByText(archivedTaskTitle, { exact: true }).click()

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })
    await detailDialog.getByRole('button', { name: '還原' }).click()

    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expectTaskOrder(page, 'todo', [firstTaskTitle, archivedTaskTitle])

    await openTaskDetail(page, 'todo', archivedTaskTitle)
    await expect(page.getByRole('dialog', { name: '任務詳細資訊' }).getByText('已還原封存任務', { exact: true })).toBeVisible()
  })

  test('使用者可以將任務移到垃圾桶，任務會離開看板並出現在垃圾桶頁', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    await moveTaskToTrashFromDrawer(page)

    await expect(page.getByRole('dialog', { name: '任務詳細資訊' })).toHaveCount(0)
    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expect(page.getByTestId('workflow-column-todo')).not.toContainText(taskTitle)
    await expect(page.getByRole('dialog', { name: /確認|刪除/ })).toHaveCount(0)

    await openTrashView(page)

    const trashedTaskItem = getLifecycleTaskItem(page, taskTitle)

    await expect(trashedTaskItem.getByText(taskTitle, { exact: true })).toBeVisible()
    await expect(trashedTaskItem.getByTestId('lifecycle-task-project-name')).toHaveText(projectName)
    await expect(trashedTaskItem.getByTestId('lifecycle-task-original-state')).toHaveText('待處理')
  })

  test('使用者可以從垃圾桶頁還原任務回原欄位最後', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const firstTaskTitle = createTaskTitle(testInfo, 'Backlog Task A')
    const trashedTaskTitle = createTaskTitle(testInfo, 'Trashed Task')

    await setupProjectBoard(page, projectName)

    await openCreateTaskModal(page)
    await createTask(page, firstTaskTitle)

    await openCreateTaskModal(page)
    await createTask(page, trashedTaskTitle)

    await openTaskDetail(page, 'todo', trashedTaskTitle)
    await moveTaskToTrashFromDrawer(page)

    await openTrashView(page)
    await page.getByText(trashedTaskTitle, { exact: true }).click()

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })
    await detailDialog.getByRole('button', { name: '還原' }).click()

    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expectTaskOrder(page, 'todo', [firstTaskTitle, trashedTaskTitle])

    await openTaskDetail(page, 'todo', trashedTaskTitle)
    await expect(page.getByRole('dialog', { name: '任務詳細資訊' }).getByText('已從垃圾桶還原', { exact: true })).toBeVisible()
  })
})
