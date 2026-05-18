import { expect, test } from '@playwright/test'
import {
  createProjectThroughApi,
  createScenarioData,
  createTask,
  createTaskThroughApi,
  createTaskTitle,
  dragTaskToColumn,
  expectTaskOrder,
  getTaskCard,
  moveTaskStateThroughApi,
  openCreateTaskModal,
  openTaskDetail,
  setupProjectBoard,
  setupTaskBoard,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Task Workflow', () => {
  test('任務卡片與 workflow 欄位提供穩定定位方式供狀態移動驗收', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)

    const taskCard = getTaskCard(page, 'todo', taskTitle)
    const activeColumn = page.getByTestId('workflow-column-active')

    await expect(taskCard).toBeVisible()
    await expect(activeColumn).toBeVisible()
  })

  test('使用者可以拖曳任務到進行中欄位，並在詳細資訊看到最新狀態', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)

    const taskCard = getTaskCard(page, 'todo', taskTitle)
    const activeColumn = page.getByTestId('workflow-column-active')

    await taskCard.dragTo(activeColumn)

    await expect(activeColumn).toContainText(taskTitle)
    await expect(page.getByTestId('workflow-column-todo')).not.toContainText(taskTitle)

    await activeColumn.getByText(taskTitle, { exact: true }).click()

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog.getByText('進行中', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('任務狀態已變更為 進行中', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('完成時間', { exact: true })).toHaveCount(0)
  })

  test('使用者可以將進行中的任務拖曳到已完成欄位並看到完成紀錄', async ({ page, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    const project = await createProjectThroughApi(request, projectName)
    const task = await createTaskThroughApi(request, project.id, taskTitle)
    await moveTaskStateThroughApi(request, project.id, task.id, 'active')

    await page.goto('/')
    await page.getByRole('button', { name: projectName }).click()
    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()

    await dragTaskToColumn(page, 'active', 'done', taskTitle)

    const doneColumn = page.getByTestId('workflow-column-done')

    await expect(doneColumn).toContainText(taskTitle)
    await expect(page.getByTestId('workflow-column-active')).not.toContainText(taskTitle)

    await openTaskDetail(page, 'done', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog.getByText('已完成', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('任務狀態已變更為 已完成', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('完成時間', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已完成任務', { exact: true })).toBeVisible()
  })

  test('拖曳後若沒有放到有效欄位，Task Card 會留在原欄位與原位置', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const firstTaskTitle = createTaskTitle(testInfo, 'Backlog Task A')
    const secondTaskTitle = createTaskTitle(testInfo, 'Backlog Task B')

    await setupProjectBoard(page, projectName)

    await openCreateTaskModal(page)
    await createTask(page, firstTaskTitle)

    await openCreateTaskModal(page)
    await createTask(page, secondTaskTitle)

    await expectTaskOrder(page, 'todo', [firstTaskTitle, secondTaskTitle])

    await getTaskCard(page, 'todo', secondTaskTitle).dragTo(page.getByRole('heading', { name: projectName }))

    await expectTaskOrder(page, 'todo', [firstTaskTitle, secondTaskTitle])
    await expect(page.getByTestId('workflow-column-active')).not.toContainText(secondTaskTitle)
  })

  test('狀態變更 API 失敗時，Task Card 會回到原欄位並顯示錯誤訊息', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await page.route('**/api/projects/*/tasks/*/state', async (route) => {
      if (route.request().method() !== 'PATCH') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'move failed' }),
      })
    })

    await setupTaskBoard(page, projectName, taskTitle)

    await getTaskCard(page, 'todo', taskTitle).dragTo(page.getByTestId('workflow-column-active'))

    await expect(page.getByTestId('workflow-column-todo')).toContainText(taskTitle)
    await expect(page.getByTestId('workflow-column-active')).not.toContainText(taskTitle)
    await expect(page.getByTestId('base-error-state')).toContainText('變更任務狀態失敗，請稍後再試。')
  })

  test('使用者可以將已完成任務重新開啟，清除目前完成時間並記錄重新開啟活動', async ({ page, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    const project = await createProjectThroughApi(request, projectName)
    const task = await createTaskThroughApi(request, project.id, taskTitle)
    await moveTaskStateThroughApi(request, project.id, task.id, 'active')
    await moveTaskStateThroughApi(request, project.id, task.id, 'done')

    await page.goto('/')
    await page.getByRole('button', { name: projectName }).click()
    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()

    await dragTaskToColumn(page, 'done', 'active', taskTitle)

    await expect(page.getByTestId('workflow-column-active')).toContainText(taskTitle)
    await expect(page.getByTestId('workflow-column-done')).not.toContainText(taskTitle)

    await openTaskDetail(page, 'active', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog.getByText('進行中', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('完成時間', { exact: true })).toHaveCount(0)
    await expect(detailDialog.getByText('已完成任務', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已重新開啟任務', { exact: true })).toBeVisible()
  })

  test('使用者可以在同欄位內拖曳調整任務順序，並以新順序反映工作優先順序', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const firstTaskTitle = createTaskTitle(testInfo, 'Priority Task A')
    const secondTaskTitle = createTaskTitle(testInfo, 'Priority Task B')

    await setupProjectBoard(page, projectName)

    await openCreateTaskModal(page)
    await createTask(page, firstTaskTitle)

    await openCreateTaskModal(page)
    await createTask(page, secondTaskTitle)

    await expectTaskOrder(page, 'todo', [firstTaskTitle, secondTaskTitle])

    await getTaskCard(page, 'todo', secondTaskTitle).dragTo(getTaskCard(page, 'todo', firstTaskTitle))

    await expectTaskOrder(page, 'todo', [secondTaskTitle, firstTaskTitle])

    await openTaskDetail(page, 'todo', secondTaskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })
    await expect(detailDialog.getByText('已調整任務順序', { exact: true })).toBeVisible()
  })
})
