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
  openProjectFromList,
  openTaskDetail,
  openTrashView,
  setupProjectBoard,
  setupTaskBoard,
} from './support/ronflowTestHelpers'
import {
  acceptInvitationThroughApi,
  createProjectThroughApi,
  createTaskThroughApi,
  getInvitationInboxThroughApi,
  inviteProjectMemberThroughApi,
  registerRonFlowApiUser,
} from './support/ronflowApiTestHelpers'
import { createRonFlowAuthUser, loginAndEnterWorkspace } from './support/ronflowAuthTestHelpers'

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

    await expect(page.getByRole('dialog', { name: '任務詳細資訊' })).toHaveCount(0)
    await expect(page.getByTestId('workflow-column-todo')).not.toContainText(archivedTaskTitle)

    await openArchivedTasksView(page)

    const archivedTaskItem = getLifecycleTaskItem(page, archivedTaskTitle)
    await archivedTaskItem.getByRole('button', { name: archivedTaskTitle, exact: true }).click()

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

    await expect(page.getByRole('dialog', { name: '任務詳細資訊' })).toHaveCount(0)
    await expect(page.getByTestId('workflow-column-todo')).not.toContainText(trashedTaskTitle)

    await openTrashView(page)

    const trashedTaskItem = getLifecycleTaskItem(page, trashedTaskTitle)
    await trashedTaskItem.getByRole('button', { name: trashedTaskTitle, exact: true }).click()

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })
    await detailDialog.getByRole('button', { name: '還原' }).click()

    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expectTaskOrder(page, 'todo', [firstTaskTitle, trashedTaskTitle])

    await openTaskDetail(page, 'todo', trashedTaskTitle)
    await expect(page.getByRole('dialog', { name: '任務詳細資訊' }).getByText('已從垃圾桶還原', { exact: true })).toBeVisible()
  })

  test('A 封存任務後，B 已開啟的 Drawer 應切換為 archived read-only 狀態', async ({ browser, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const ownerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))
    const memberSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('member'))
    const project = await createProjectThroughApi(request, ownerSession, projectName)

    await createTaskThroughApi(request, ownerSession, project.id, taskTitle)
    await inviteProjectMemberThroughApi(request, ownerSession, project.id, memberSession.user.email)

    const [pendingInvitation] = await getInvitationInboxThroughApi(request, memberSession)
    await acceptInvitationThroughApi(request, memberSession, pendingInvitation.id)

    const ownerContext = await browser.newContext()
    const memberContext = await browser.newContext()
    const ownerPage = await ownerContext.newPage()
    const memberPage = await memberContext.newPage()

    await loginAndEnterWorkspace(ownerPage, ownerSession.user)
    await openProjectFromList(ownerPage, projectName)
    await openTaskDetail(ownerPage, 'todo', taskTitle)

    await loginAndEnterWorkspace(memberPage, memberSession.user)
    await openProjectFromList(memberPage, projectName)
    await openTaskDetail(memberPage, 'todo', taskTitle)

    await archiveTaskFromDrawer(ownerPage)

    const memberDetailDialog = memberPage.getByRole('dialog', { name: '任務詳細資訊' })
    await expect(memberPage.getByTestId('workflow-column-todo')).not.toContainText(taskTitle, { timeout: 10000 })
    await expect(memberDetailDialog.getByText('此任務已封存', { exact: true })).toBeVisible({ timeout: 10000 })
    await expect(memberDetailDialog.getByRole('button', { name: '編輯', exact: true })).toHaveCount(0)
    await expect(memberDetailDialog.getByRole('button', { name: '還原', exact: true })).toBeVisible()

    await ownerContext.close()
    await memberContext.close()
  })

  test('A 還原已封存任務後，B 已開啟的 archived Drawer 應切回 active 可查看狀態', async ({ browser, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const ownerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))
    const memberSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('member'))
    const project = await createProjectThroughApi(request, ownerSession, projectName)

    await createTaskThroughApi(request, ownerSession, project.id, taskTitle)
    await inviteProjectMemberThroughApi(request, ownerSession, project.id, memberSession.user.email)

    const [pendingInvitation] = await getInvitationInboxThroughApi(request, memberSession)
    await acceptInvitationThroughApi(request, memberSession, pendingInvitation.id)

    const ownerContext = await browser.newContext()
    const memberContext = await browser.newContext()
    const ownerPage = await ownerContext.newPage()
    const memberPage = await memberContext.newPage()

    await loginAndEnterWorkspace(ownerPage, ownerSession.user)
    await openProjectFromList(ownerPage, projectName)
    await loginAndEnterWorkspace(memberPage, memberSession.user)
    await openProjectFromList(memberPage, projectName)
    await openTaskDetail(memberPage, 'todo', taskTitle)

    await openTaskDetail(ownerPage, 'todo', taskTitle)
    await archiveTaskFromDrawer(ownerPage)
    await expect(memberPage.getByRole('dialog', { name: '任務詳細資訊' }).getByText('此任務已封存', { exact: true })).toBeVisible({ timeout: 10000 })

    await openArchivedTasksView(ownerPage)
    await getLifecycleTaskItem(ownerPage, taskTitle).getByRole('button', { name: '還原', exact: true }).click()

    const memberDetailDialog = memberPage.getByRole('dialog', { name: '任務詳細資訊' })
    await expect(memberPage.getByTestId('workflow-column-todo')).toContainText(taskTitle, { timeout: 10000 })
    await expect(memberDetailDialog.getByText('此任務已封存', { exact: true })).toHaveCount(0, { timeout: 10000 })
    await expect(memberDetailDialog.getByRole('button', { name: '編輯', exact: true })).toBeVisible({ timeout: 10000 })
    await expect(memberDetailDialog.getByRole('button', { name: '還原', exact: true })).toHaveCount(0)

    await ownerContext.close()
    await memberContext.close()
  })
})
