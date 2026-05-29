import { expect, test } from '@playwright/test'
import {
  createProject,
  createScenarioData,
  createTask,
  createTaskTitle,
  openInvitationInbox,
  openCreateProjectModal,
  openCreateTaskModal,
  openProjectFromList,
  openTaskDetail,
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
import { createRonFlowAuthUser, loginAndEnterWorkspace, registerAndEnterWorkspace } from './support/ronflowAuthTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Task Detail Behavior', () => {
  test('同一使用者在新 tab 成功載入 workspace 後，舊 tab 應顯示 session 已失效訊息並自動導回登入頁', async ({ browser }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const user = createRonFlowAuthUser('owner')
    const context = await browser.newContext()
    const firstPage = await context.newPage()

    await registerAndEnterWorkspace(firstPage, user)
    await openCreateProjectModal(firstPage)
    await createProject(firstPage, projectName)
    await openCreateTaskModal(firstPage)
    await createTask(firstPage, taskTitle)
    await openTaskDetail(firstPage, 'todo', taskTitle)
    await firstPage.getByRole('dialog', { name: '任務詳細資訊' }).getByRole('button', { name: '編輯', exact: true }).click()

    const secondPage = await context.newPage()
    await secondPage.goto('/')

    await expect(secondPage.getByRole('button', { name: '登出' })).toBeVisible()
    await expect(firstPage.getByText(/session 已失效|工作階段已失效/)).toBeVisible({ timeout: 10000 })
    await expect(firstPage.getByRole('heading', { name: '登入 RonFlow' })).toBeVisible({ timeout: 10000 })
    await expect(firstPage.getByRole('button', { name: '登出' })).toHaveCount(0)

    await context.close()
  })

  test('同一使用者建立新 RonFlow session 後，舊 session 持有的 content edit lock 應立即釋放，其他成員可重新進入 edit mode', async ({ browser, request }, testInfo) => {
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

    const ownerDetailDialog = ownerPage.getByRole('dialog', { name: '任務詳細資訊' })
    await ownerDetailDialog.getByRole('button', { name: '編輯', exact: true }).click()

    await loginAndEnterWorkspace(memberPage, memberSession.user)
    await openProjectFromList(memberPage, projectName)
    await openTaskDetail(memberPage, 'todo', taskTitle)

    const memberDetailDialog = memberPage.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(memberDetailDialog.getByRole('button', { name: '編輯', exact: true })).toBeDisabled()

    const secondOwnerPage = await ownerContext.newPage()
    await secondOwnerPage.goto('/')

    await expect(secondOwnerPage.getByRole('button', { name: '登出' })).toBeVisible()
    await expect(ownerPage.getByText(/session 已失效|工作階段已失效/)).toBeVisible({ timeout: 10000 })
    await expect(ownerPage.getByRole('heading', { name: '登入 RonFlow' })).toBeVisible({ timeout: 10000 })
    await expect(memberDetailDialog.getByRole('button', { name: '編輯', exact: true })).toBeEnabled({ timeout: 10000 })

    await memberDetailDialog.getByRole('button', { name: '編輯', exact: true }).click()
    await expect(memberDetailDialog.getByRole('button', { name: '儲存變更', exact: true })).toBeVisible()

    await ownerContext.close()
    await memberContext.close()
  })

  test('重新整理後可透過 session restore 還原登入並看到已建立的專案與任務', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)

    await page.evaluate(() => {
      window.localStorage.clear()
      window.sessionStorage.clear()
    })
    await page.reload()

    await expect(page.getByRole('button', { name: '登出' })).toBeVisible()
    await page.getByRole('button', { name: projectName }).click()
    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expect(page.getByTestId('workflow-column-todo')).toContainText(taskTitle)
  })

  test('使用者可以在 Drawer 修改任務標題、描述與到期日，並看到最新資料與活動紀錄', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const updatedTaskTitle = createTaskTitle(testInfo, 'Updated Task Title')
    const updatedDescription = '支援在 Drawer 直接編輯任務內容與活動紀錄'
    const updatedDueDate = '2026-05-20'

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await detailDialog.getByRole('button', { name: '編輯', exact: true }).click()

    await detailDialog.getByLabel('任務標題').fill(updatedTaskTitle)
    await detailDialog.getByLabel('任務描述').fill(updatedDescription)
    await detailDialog.getByLabel('到期日').fill(updatedDueDate)
    await detailDialog.getByRole('button', { name: '儲存變更' }).click()

    await expect(detailDialog.getByRole('button', { name: '編輯', exact: true })).toBeVisible()
    await expect(detailDialog.getByRole('button', { name: '儲存變更', exact: true })).toHaveCount(0)
    await expect(detailDialog.getByLabel('任務標題')).toHaveValue(updatedTaskTitle)
    await expect(detailDialog.getByLabel('任務標題')).toBeDisabled()
    await expect(detailDialog.getByLabel('任務描述')).toHaveValue(updatedDescription)
    await expect(detailDialog.getByLabel('任務描述')).toBeDisabled()
    await expect(detailDialog.getByLabel('到期日')).toHaveValue(updatedDueDate)
    await expect(detailDialog.getByLabel('到期日')).toBeDisabled()
    await expect(detailDialog.getByText('已更新任務標題', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已更新任務描述', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已設定到期日為 2026/05/20', { exact: true })).toBeVisible()
    await expect(page.getByTestId('workflow-column-todo')).toContainText(updatedTaskTitle)
    await expect(page.getByTestId('workflow-column-todo')).not.toContainText(taskTitle)
  })

  test('project template 建立後，新 task 應繼承 checklist，view mode 全部勾選後自動進入 review', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const user = createRonFlowAuthUser('owner')

    await registerAndEnterWorkspace(page, user)
    await openCreateProjectModal(page)
    await createProject(page, projectName)

    await page.getByRole('button', { name: '完成條件模板' }).click()

    const templatesDialog = page.getByRole('dialog', { name: '完成條件模板' })
    await templatesDialog.getByRole('button', { name: '新增模板' }).click()
    await templatesDialog.getByRole('button', { name: '新增模板' }).click()
    await templatesDialog.getByRole('textbox').nth(0).fill('需求已釐清')
    await templatesDialog.getByRole('textbox').nth(1).fill('驗收測試已撰寫')
    await templatesDialog.getByRole('button', { name: '儲存模板' }).click()
    await expect(templatesDialog).toHaveCount(0)

    await openCreateTaskModal(page)
    await createTask(page, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })
    await expect(detailDialog.getByTestId('task-subtask-item').nth(0).getByRole('textbox')).toHaveValue('需求已釐清')
    await expect(detailDialog.getByTestId('task-subtask-item').nth(1).getByRole('textbox')).toHaveValue('驗收測試已撰寫')

    await detailDialog.getByTestId('task-subtask-item').nth(0).getByRole('checkbox').check()
    await detailDialog.getByTestId('task-subtask-item').nth(1).getByRole('checkbox').check()

    await expect(page.getByTestId('workflow-column-review')).toContainText(taskTitle, { timeout: 10000 })
    await expect(page.getByTestId('workflow-column-done')).not.toContainText(taskTitle)
  })

  test('A 進入 edit mode 後，B 仍可查看同一個 Task，但編輯按鈕應 disabled', async ({ browser, request }, testInfo) => {
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

    const ownerDetailDialog = ownerPage.getByRole('dialog', { name: '任務詳細資訊' })
    await ownerDetailDialog.getByRole('button', { name: '編輯', exact: true }).click()

    await loginAndEnterWorkspace(memberPage, memberSession.user)
    await openProjectFromList(memberPage, projectName)
    await openTaskDetail(memberPage, 'todo', taskTitle)

    const memberDetailDialog = memberPage.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(memberDetailDialog.getByLabel('任務標題')).toHaveValue(taskTitle)
    await expect(memberDetailDialog.getByRole('button', { name: '編輯', exact: true })).toBeDisabled()
    await expect(memberDetailDialog.getByRole('button', { name: '儲存變更', exact: true })).toHaveCount(0)
    await expect(memberDetailDialog.getByLabel('任務標題')).toBeDisabled()
    await expect(memberDetailDialog.getByLabel('任務描述')).toBeDisabled()
    await expect(memberDetailDialog.getByLabel('到期日')).toBeDisabled()

    await ownerContext.close()
    await memberContext.close()
  })

  test('A 儲存 Task 標題後，B 的 view mode Drawer 與 Board card 應在 10 秒內同步最新已儲存內容', async ({ browser, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const updatedTaskTitle = createTaskTitle(testInfo, 'Realtime Updated Task Title')
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

    const ownerDetailDialog = ownerPage.getByRole('dialog', { name: '任務詳細資訊' })
    const memberDetailDialog = memberPage.getByRole('dialog', { name: '任務詳細資訊' })

    await ownerDetailDialog.getByRole('button', { name: '編輯', exact: true }).click()
    await ownerDetailDialog.getByLabel('任務標題').fill(updatedTaskTitle)
    await ownerDetailDialog.getByRole('button', { name: '儲存變更' }).click()

    await expect(memberDetailDialog.getByLabel('任務標題')).toHaveValue(updatedTaskTitle, { timeout: 10000 })
    await expect(memberPage.getByTestId('workflow-column-todo')).toContainText(updatedTaskTitle, { timeout: 10000 })
    await expect(memberPage.getByTestId('workflow-column-todo')).not.toContainText(taskTitle)

    await ownerContext.close()
    await memberContext.close()
  })

  test('A 儲存成功後，content edit lock 應立即釋放，B 可重新進入 edit mode', async ({ browser, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const updatedTaskTitle = createTaskTitle(testInfo, 'Released Lock Task Title')
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

    const ownerDetailDialog = ownerPage.getByRole('dialog', { name: '任務詳細資訊' })
    const memberDetailDialog = memberPage.getByRole('dialog', { name: '任務詳細資訊' })

    await ownerDetailDialog.getByRole('button', { name: '編輯', exact: true }).click()
    await ownerDetailDialog.getByLabel('任務標題').fill(updatedTaskTitle)

    await expect(memberDetailDialog.getByRole('button', { name: '編輯', exact: true })).toBeDisabled()

    await ownerDetailDialog.getByRole('button', { name: '儲存變更', exact: true }).click()

    await expect(memberDetailDialog.getByLabel('任務標題')).toHaveValue(updatedTaskTitle, { timeout: 10000 })
    await expect(memberDetailDialog.getByRole('button', { name: '編輯', exact: true })).toBeEnabled({ timeout: 10000 })

    await memberDetailDialog.getByRole('button', { name: '編輯', exact: true }).click()
    await expect(memberDetailDialog.getByRole('button', { name: '儲存變更', exact: true })).toBeVisible()

    await ownerContext.close()
    await memberContext.close()
  })

  test('A 持有 content edit lock 時，B 在 view mode 中仍可查看 Task，但更多操作應被 disabled', async ({ browser, request }, testInfo) => {
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
    await ownerPage.getByRole('dialog', { name: '任務詳細資訊' }).getByRole('button', { name: '編輯', exact: true }).click()

    await loginAndEnterWorkspace(memberPage, memberSession.user)
    await openProjectFromList(memberPage, projectName)
    await openTaskDetail(memberPage, 'todo', taskTitle)

    const memberDetailDialog = memberPage.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(memberDetailDialog.getByRole('button', { name: /更多|更多操作/ })).toBeDisabled()

    await ownerContext.close()
    await memberContext.close()
  })

  test('A 切換到另一個 Project 後，舊 Project 的 content edit lock 應釋放，B 可重新進入 edit mode', async ({ browser, request }, testInfo) => {
    const suffix = `${testInfo.workerIndex}-${testInfo.retry}-${Date.now()}`
    const sourceProjectName = `Lock Source Project ${suffix}`
    const targetProjectName = `Lock Target Project ${suffix}`
    const taskTitle = `Locked Task ${suffix}`
    const ownerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))
    const memberSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('member'))
    const sourceProject = await createProjectThroughApi(request, ownerSession, sourceProjectName)
    await createProjectThroughApi(request, ownerSession, targetProjectName)

    await createTaskThroughApi(request, ownerSession, sourceProject.id, taskTitle)
    await inviteProjectMemberThroughApi(request, ownerSession, sourceProject.id, memberSession.user.email)

    const [pendingInvitation] = await getInvitationInboxThroughApi(request, memberSession)
    await acceptInvitationThroughApi(request, memberSession, pendingInvitation.id)

    const ownerContext = await browser.newContext()
    const memberContext = await browser.newContext()
    const ownerPage = await ownerContext.newPage()
    const memberPage = await memberContext.newPage()

    await loginAndEnterWorkspace(ownerPage, ownerSession.user)
    await openProjectFromList(ownerPage, sourceProjectName)
    await openTaskDetail(ownerPage, 'todo', taskTitle)
    await ownerPage.getByRole('dialog', { name: '任務詳細資訊' }).getByRole('button', { name: '編輯', exact: true }).click()

    await loginAndEnterWorkspace(memberPage, memberSession.user)
    await openProjectFromList(memberPage, sourceProjectName)
    await openTaskDetail(memberPage, 'todo', taskTitle)

    const memberDetailDialog = memberPage.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(memberDetailDialog.getByRole('button', { name: '編輯', exact: true })).toBeDisabled()

    await openProjectFromList(ownerPage, targetProjectName)
    await expect(ownerPage.getByRole('heading', { name: targetProjectName })).toBeVisible()

    await expect(memberDetailDialog.getByRole('button', { name: '編輯', exact: true })).toBeEnabled({ timeout: 10000 })

    await ownerContext.close()
    await memberContext.close()
  })

  test('A 持有 content edit lock 超過 30 秒無活動後，應自動退出 edit mode、丟棄未儲存草稿，並讓 B 可重新進入 edit mode', async ({ browser, request }, testInfo) => {
    test.slow()

    const { projectName, taskTitle } = createScenarioData(testInfo)
    const draftTaskTitle = createTaskTitle(testInfo, 'Unsaved Draft Title')
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

    const ownerDetailDialog = ownerPage.getByRole('dialog', { name: '任務詳細資訊' })
    const memberDetailDialog = memberPage.getByRole('dialog', { name: '任務詳細資訊' })

    await ownerDetailDialog.getByRole('button', { name: '編輯', exact: true }).click()
    await ownerDetailDialog.getByLabel('任務標題').fill(draftTaskTitle)

    await expect(memberDetailDialog.getByRole('button', { name: '編輯', exact: true })).toBeDisabled()

    await ownerPage.waitForTimeout(35000)

    await expect(ownerPage.getByRole('button', { name: '登出' })).toBeVisible()
    await expect(ownerDetailDialog.getByRole('button', { name: '編輯', exact: true })).toBeVisible({ timeout: 10000 })
    await expect(ownerDetailDialog.getByRole('button', { name: '儲存變更', exact: true })).toHaveCount(0)
    await expect(ownerDetailDialog.getByLabel('任務標題')).toHaveValue(taskTitle)
    await expect(ownerDetailDialog.getByLabel('任務標題')).toBeDisabled()
    await expect(memberDetailDialog.getByRole('button', { name: '編輯', exact: true })).toBeEnabled({ timeout: 10000 })

    await memberDetailDialog.getByRole('button', { name: '編輯', exact: true }).click()
    await expect(memberDetailDialog.getByRole('button', { name: '儲存變更', exact: true })).toBeVisible()

    await ownerContext.close()
    await memberContext.close()
  })

  test('A 進入邀請收件匣後，舊 Project 的 content edit lock 應立即釋放，B 可重新進入 edit mode', async ({ browser, request }, testInfo) => {
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
    await ownerPage.getByRole('dialog', { name: '任務詳細資訊' }).getByRole('button', { name: '編輯', exact: true }).click()

    await loginAndEnterWorkspace(memberPage, memberSession.user)
    await openProjectFromList(memberPage, projectName)
    await openTaskDetail(memberPage, 'todo', taskTitle)

    const memberDetailDialog = memberPage.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(memberDetailDialog.getByRole('button', { name: '編輯', exact: true })).toBeDisabled()

    await openInvitationInbox(ownerPage)
    await expect(ownerPage.getByRole('heading', { name: '邀請收件匣' })).toBeVisible()

    await expect(memberDetailDialog.getByRole('button', { name: '編輯', exact: true })).toBeEnabled({ timeout: 10000 })

    await memberDetailDialog.getByRole('button', { name: '編輯', exact: true }).click()
    await expect(memberDetailDialog.getByRole('button', { name: '儲存變更', exact: true })).toBeVisible()

    await ownerContext.close()
    await memberContext.close()
  })
})
