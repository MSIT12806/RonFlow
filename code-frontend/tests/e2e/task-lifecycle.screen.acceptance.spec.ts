import { expect, test } from '@playwright/test'
import {
  archiveTaskFromDrawer,
  createScenarioData,
  createTask,
  createTaskTitle,
  moveTaskToTrashFromDrawer,
  openArchivedTasksView,
  openCreateTaskModal,
  openTaskDetail,
  openTrashView,
  setupProjectBoard,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Task Lifecycle Screen', () => {
  test('專案看板提供進入已封存任務與垃圾桶的入口', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)

    await expect(page.getByRole('button', { name: '已封存任務' })).toBeVisible()
    await expect(page.getByRole('button', { name: '垃圾桶' })).toBeVisible()
  })

  test('已封存任務頁在沒有任務時顯示空狀態', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)
    await openArchivedTasksView(page)

    await expect(page.getByText('目前沒有已封存任務')).toBeVisible()
  })

  test('垃圾桶頁在沒有任務時顯示空狀態', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)
    await openTrashView(page)

    await expect(page.getByText('垃圾桶目前沒有任務')).toBeVisible()
  })

  test('已封存任務的 read-only Drawer 顯示正確畫面狀態', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const archivedTaskTitle = createTaskTitle(testInfo, 'Archived Task')

    await setupProjectBoard(page, projectName)
    await openCreateTaskModal(page)
    await createTask(page, archivedTaskTitle)
    await openTaskDetail(page, 'todo', archivedTaskTitle)
    await archiveTaskFromDrawer(page)

    await openArchivedTasksView(page)
    await page.getByText(archivedTaskTitle, { exact: true }).click()

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog.getByText('此任務已封存', { exact: true })).toBeVisible()
    await expect(detailDialog.getByLabel('任務標題')).toBeDisabled()
    await expect(detailDialog.getByLabel('任務描述')).toBeDisabled()
    await expect(detailDialog.getByLabel('到期日')).toBeDisabled()
    await expect(detailDialog.getByRole('button', { name: '儲存變更' })).toHaveCount(0)
    await expect(detailDialog.getByText('已封存任務', { exact: true })).toBeVisible()
    await expect(detailDialog.getByRole('button', { name: '還原' })).toBeVisible()
  })

  test('垃圾桶任務的 read-only Drawer 顯示正確畫面狀態', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const trashedTaskTitle = createTaskTitle(testInfo, 'Trashed Task')

    await setupProjectBoard(page, projectName)
    await openCreateTaskModal(page)
    await createTask(page, trashedTaskTitle)
    await openTaskDetail(page, 'todo', trashedTaskTitle)
    await moveTaskToTrashFromDrawer(page)

    await openTrashView(page)
    await page.getByText(trashedTaskTitle, { exact: true }).click()

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog.getByText('此任務位於垃圾桶', { exact: true })).toBeVisible()
    await expect(detailDialog.getByLabel('任務標題')).toBeDisabled()
    await expect(detailDialog.getByLabel('任務描述')).toBeDisabled()
    await expect(detailDialog.getByLabel('到期日')).toBeDisabled()
    await expect(detailDialog.getByRole('button', { name: '儲存變更' })).toHaveCount(0)
    await expect(detailDialog.getByText('已移到垃圾桶', { exact: true })).toBeVisible()
    await expect(detailDialog.getByRole('button', { name: '還原' })).toBeVisible()
  })
})
