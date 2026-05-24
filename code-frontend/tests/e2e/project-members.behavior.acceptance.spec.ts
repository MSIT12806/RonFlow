import { expect, test, type APIRequestContext, type Browser, type Page } from '@playwright/test'
import {
  acceptInvitationThroughApi,
  configureTestFaultsThroughApi,
  createProjectThroughApi,
  createTaskThroughApi,
  getInvitationInboxThroughApi,
  getProjectInvitationsThroughApi,
  inviteProjectMemberThroughApi,
  registerRonFlowApiUser,
} from './support/ronflowApiTestHelpers'
import { createRonFlowAuthUser, loginAndEnterWorkspace } from './support/ronflowAuthTestHelpers'
import {
  createScenarioData,
  openInvitationInbox,
  openProjectFromList,
  openProjectMembersPanel,
  openTaskDetail,
} from './support/ronflowTestHelpers'

async function loginOwnerAndOpenMembersPanel(page: Page, request: APIRequestContext, projectName: string) {
  const ownerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))
  const project = await createProjectThroughApi(request, ownerSession, projectName)

  await loginAndEnterWorkspace(page, ownerSession.user)
  await openProjectFromList(page, projectName)
  await openProjectMembersPanel(page)

  return { ownerSession, project }
}

async function setupPresenceObservation(browser: Browser, request: APIRequestContext, projectName: string, taskTitle?: string) {
  const ownerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))
  const memberSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('member'))
  const project = await createProjectThroughApi(request, ownerSession, projectName)

  if (taskTitle) {
    await createTaskThroughApi(request, ownerSession, project.id, taskTitle)
  }

  await inviteProjectMemberThroughApi(request, ownerSession, project.id, memberSession.user.email)

  const invitation = (await getInvitationInboxThroughApi(request, memberSession)).find(
    (item) => item.projectId === project.id,
  )

  expect(invitation).toBeDefined()

  await acceptInvitationThroughApi(request, memberSession, invitation!.id)

  const ownerContext = await browser.newContext()
  const memberContext = await browser.newContext()
  const ownerPage = await ownerContext.newPage()
  const memberPage = await memberContext.newPage()

  await loginAndEnterWorkspace(ownerPage, ownerSession.user)
  await openProjectFromList(ownerPage, projectName)
  await openProjectMembersPanel(ownerPage)

  await loginAndEnterWorkspace(memberPage, memberSession.user)
  await openProjectFromList(memberPage, projectName)

  return {
    ownerPage,
    memberPage,
    ownerContext,
    memberContext,
    ownerSession,
    memberSession,
    project,
  }
}

async function setupCrossProjectPresenceObservation(browser: Browser, request: APIRequestContext, firstProjectName: string, secondProjectName: string) {
  const firstOwnerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner-a'))
  const secondOwnerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner-b'))
  const memberSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('member'))

  const firstProject = await createProjectThroughApi(request, firstOwnerSession, firstProjectName)
  const secondProject = await createProjectThroughApi(request, secondOwnerSession, secondProjectName)

  await inviteProjectMemberThroughApi(request, firstOwnerSession, firstProject.id, memberSession.user.email)
  await inviteProjectMemberThroughApi(request, secondOwnerSession, secondProject.id, memberSession.user.email)

  const invitations = await getInvitationInboxThroughApi(request, memberSession)
  const firstInvitation = invitations.find((item) => item.projectId === firstProject.id)
  const secondInvitation = invitations.find((item) => item.projectId === secondProject.id)

  expect(firstInvitation).toBeDefined()
  expect(secondInvitation).toBeDefined()

  await acceptInvitationThroughApi(request, memberSession, firstInvitation!.id)
  await acceptInvitationThroughApi(request, memberSession, secondInvitation!.id)

  const firstOwnerContext = await browser.newContext()
  const secondOwnerContext = await browser.newContext()
  const memberContext = await browser.newContext()
  const firstOwnerPage = await firstOwnerContext.newPage()
  const secondOwnerPage = await secondOwnerContext.newPage()
  const memberPage = await memberContext.newPage()

  await loginAndEnterWorkspace(firstOwnerPage, firstOwnerSession.user)
  await openProjectFromList(firstOwnerPage, firstProjectName)
  await openProjectMembersPanel(firstOwnerPage)

  await loginAndEnterWorkspace(secondOwnerPage, secondOwnerSession.user)
  await openProjectFromList(secondOwnerPage, secondProjectName)
  await openProjectMembersPanel(secondOwnerPage)

  await loginAndEnterWorkspace(memberPage, memberSession.user)
  await openProjectFromList(memberPage, firstProjectName)

  return {
    firstOwnerPage,
    secondOwnerPage,
    memberPage,
    firstOwnerContext,
    secondOwnerContext,
    memberContext,
    memberSession,
  }
}

function getOnlineUsersSection(page: Page) {
  return page.getByTestId('project-online-users')
}

function getOnlineUserItem(page: Page, userName: string) {
  return getOnlineUsersSection(page).getByRole('listitem').filter({ hasText: userName }).first()
}

test.describe('RonFlow UI/UX 驗收規格 - Project Members Behavior', () => {
  test('邀請對象為必填', async ({ page, request }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await loginOwnerAndOpenMembersPanel(page, request, projectName)

    await page.getByRole('button', { name: '邀請', exact: true }).click()

    await expect(page.getByText('邀請對象為必填欄位', { exact: true })).toBeVisible()
  })

  test('Project Owner 可以送出邀請並在待處理清單看到結果', async ({ page, request }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const inviteeSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('teammate'))
    const { ownerSession, project } = await loginOwnerAndOpenMembersPanel(page, request, projectName)

    const inviteeInput = page.getByLabel('邀請對象')

    await inviteeInput.fill(inviteeSession.user.email)
    await page.getByRole('button', { name: '邀請', exact: true }).click()

    await expect(page.getByText(inviteeSession.user.email, { exact: true })).toBeVisible()
    await expect(inviteeInput).toHaveValue('')

    const invitations = await getProjectInvitationsThroughApi(request, ownerSession, project.id)
    expect(invitations).toHaveLength(1)
    expect(invitations[0]?.invitee).toBe(inviteeSession.user.email)
  })

  test('邀請失敗時保留輸入內容並顯示錯誤訊息', async ({ page, request }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const inviteeSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('teammate'))
    const { ownerSession } = await loginOwnerAndOpenMembersPanel(page, request, projectName)

    await configureTestFaultsThroughApi(request, ownerSession, [{
      method: 'POST',
      pathPattern: '/api/projects/*/invitations',
      statusCode: 500,
      delayMs: 1200,
      message: 'invite failed',
    }])

    const inviteeInput = page.getByLabel('邀請對象')
    const inviteButton = page.getByRole('button', { name: '邀請', exact: true })

    await inviteeInput.fill(inviteeSession.user.email)
    await inviteButton.click()

    await expect(inviteeInput).toBeDisabled()
    await expect(inviteButton).toBeDisabled()
    await expect(page.getByText('邀請成員失敗，請稍後再試。', { exact: true })).toBeVisible()
    await expect(inviteeInput).toHaveValue(inviteeSession.user.email)
  })

  test('邀請送出進行中時會鎖定輸入與按鈕，避免重複送出', async ({ page, request }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const inviteeSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('teammate'))
    const { ownerSession, project } = await loginOwnerAndOpenMembersPanel(page, request, projectName)

    await configureTestFaultsThroughApi(request, ownerSession, [{
      method: 'POST',
      pathPattern: '/api/projects/*/invitations',
      statusCode: null,
      delayMs: 1200,
    }])

    const inviteeInput = page.getByLabel('邀請對象')
    const inviteButton = page.getByRole('button', { name: '邀請', exact: true })

    await inviteeInput.fill(inviteeSession.user.email)
    await inviteButton.dblclick()

    await expect(inviteeInput).toBeDisabled()
    await expect(inviteButton).toBeDisabled()
    await expect(page.getByText(inviteeSession.user.email, { exact: true })).toBeVisible()

    const invitations = await getProjectInvitationsThroughApi(request, ownerSession, project.id)
    expect(invitations.filter((invitation) => invitation.invitee === inviteeSession.user.email)).toHaveLength(1)
  })

  test('嘗試邀請已是目前專案成員的使用者時，顯示重複邀請錯誤訊息', async ({ page, request }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const ownerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))
    const memberSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('member'))
    const project = await createProjectThroughApi(request, ownerSession, projectName)

    await inviteProjectMemberThroughApi(request, ownerSession, project.id, memberSession.user.email)

    const invitation = (await getInvitationInboxThroughApi(request, memberSession))[0]
    await acceptInvitationThroughApi(request, memberSession, invitation.id)

    await loginAndEnterWorkspace(page, ownerSession.user)
    await openProjectFromList(page, projectName)
    await openProjectMembersPanel(page)

    const inviteeInput = page.getByLabel('邀請對象')

    await inviteeInput.fill(memberSession.user.email)
    await page.getByRole('button', { name: '邀請', exact: true }).click()

    await expect(page.getByText('該使用者已是目前專案成員', { exact: true })).toBeVisible()
    await expect(inviteeInput).toHaveValue(memberSession.user.email)
  })

  test('邀請不存在的使用者時，顯示找不到可邀請使用者', async ({ page, request }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const unknownUser = `unknown-${Date.now()}@example.test`

    await loginOwnerAndOpenMembersPanel(page, request, projectName)

    const inviteeInput = page.getByLabel('邀請對象')

    await inviteeInput.fill(unknownUser)
    await page.getByRole('button', { name: '邀請', exact: true }).click()

    await expect(page.getByText('找不到可邀請的使用者', { exact: true })).toBeVisible()
    await expect(inviteeInput).toHaveValue(unknownUser)
  })

  test('專案成員面板載入失敗時，顯示 section-level 錯誤訊息而不是一般內容', async ({ page, request }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const ownerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))

    await createProjectThroughApi(request, ownerSession, projectName)
    await configureTestFaultsThroughApi(request, ownerSession, [
      {
        method: 'GET',
        pathPattern: '/api/projects/*/members',
        statusCode: 500,
        message: 'members load failed',
      },
      {
        method: 'GET',
        pathPattern: '/api/projects/*/invitations',
        statusCode: 500,
        message: 'invitations load failed',
      },
    ])

    await loginAndEnterWorkspace(page, ownerSession.user)
    await openProjectFromList(page, projectName)
    await openProjectMembersPanel(page)

    await expect(page.getByTestId('base-error-state')).toContainText('無法載入專案成員資料，請稍後再試。')
    await expect(page.getByText('目前沒有待處理邀請', { exact: true })).toHaveCount(0)
  })

  test('成員查詢成功但 invitations 查詢失敗時，保留成員區塊並顯示待處理邀請錯誤', async ({ page, request }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const ownerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))

    await createProjectThroughApi(request, ownerSession, projectName)
    await configureTestFaultsThroughApi(request, ownerSession, [{
      method: 'GET',
      pathPattern: '/api/projects/*/invitations',
      statusCode: 500,
      message: 'invitations load failed',
    }])

    await loginAndEnterWorkspace(page, ownerSession.user)
    await openProjectFromList(page, projectName)
    await openProjectMembersPanel(page)

    await expect(page.getByTestId('project-members-list').getByRole('listitem').filter({ hasText: ownerSession.user.userName })).toBeVisible()
    await expect(page.getByLabel('邀請對象')).toBeVisible()
    await expect(page.getByTestId('base-error-state')).toContainText('無法載入待處理邀請，請稍後再試。')
    await expect(page.getByText('目前沒有待處理邀請', { exact: true })).toHaveCount(0)
    await expect(page.getByText('無法載入專案成員資料，請稍後再試。', { exact: true })).toHaveCount(0)
  })

  test('non-owner 在專案看板上看不到成員面板入口，因此無法操作邀請', async ({ page, request }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const ownerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))
    const memberSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('member'))
    const project = await createProjectThroughApi(request, ownerSession, projectName)

    await inviteProjectMemberThroughApi(request, ownerSession, project.id, memberSession.user.email)

    const invitation = (await getInvitationInboxThroughApi(request, memberSession))[0]
    await acceptInvitationThroughApi(request, memberSession, invitation.id)

    await loginAndEnterWorkspace(page, memberSession.user)
    await openProjectFromList(page, projectName)

    await expect(page.getByRole('button', { name: '專案成員', exact: true })).toHaveCount(0)
    await expect(page.getByLabel('邀請對象')).toHaveCount(0)
  })

  test('待處理邀請被接受後，Project Members Panel 應在 10 秒內自動把成員從 pending 轉入 members list', async ({ page, request }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const memberSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('member'))
    const { ownerSession, project } = await loginOwnerAndOpenMembersPanel(page, request, projectName)

    const inviteeInput = page.getByLabel('邀請對象')

    await inviteeInput.fill(memberSession.user.email)
    await page.getByRole('button', { name: '邀請', exact: true }).click()

    await expect(page.getByText(memberSession.user.email, { exact: true })).toBeVisible()

    const invitation = (await getInvitationInboxThroughApi(request, memberSession)).find(
      (item) => item.projectId === project.id,
    )

    expect(invitation).toBeDefined()

    await acceptInvitationThroughApi(request, memberSession, invitation!.id)

    await expect(
      page.getByTestId('project-members-list').getByRole('listitem').filter({ hasText: memberSession.user.userName }),
    ).toBeVisible({ timeout: 10000 })
    await expect(page.getByText(memberSession.user.email, { exact: true })).toHaveCount(0)
    await expect(page.getByText('目前沒有待處理邀請', { exact: true })).toBeVisible()
  })

  test('成員離開 project scope 後，在線名單應於 10 秒內移除該成員', async ({ browser, request }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const { ownerPage, memberPage, ownerContext, memberContext, memberSession } = await setupPresenceObservation(browser, request, projectName)

    try {
      await expect(getOnlineUserItem(ownerPage, memberSession.user.userName)).toBeVisible({ timeout: 10000 })

      await openInvitationInbox(memberPage)

      await expect(getOnlineUserItem(ownerPage, memberSession.user.userName)).toHaveCount(0, { timeout: 10000 })
    } finally {
      await Promise.all([ownerContext.close(), memberContext.close()])
    }
  })

  test('成員登出後，在線名單應於 10 秒內移除該成員', async ({ browser, request }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const { ownerPage, memberPage, ownerContext, memberContext, memberSession } = await setupPresenceObservation(browser, request, projectName)

    try {
      await expect(getOnlineUserItem(ownerPage, memberSession.user.userName)).toBeVisible({ timeout: 10000 })

      await memberPage.getByRole('button', { name: '登出' }).click()
      await expect(memberPage.getByRole('heading', { name: '登入 RonFlow' })).toBeVisible()

      await expect(getOnlineUserItem(ownerPage, memberSession.user.userName)).toHaveCount(0, { timeout: 10000 })
    } finally {
      await Promise.all([ownerContext.close(), memberContext.close()])
    }
  })

  test('成員關閉 Task Detail Drawer 但仍停留在 Project Board 時，在線名單應保留該成員', async ({ browser, request }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const { ownerPage, memberPage, ownerContext, memberContext, memberSession } = await setupPresenceObservation(browser, request, projectName, taskTitle)

    try {
      await expect(getOnlineUserItem(ownerPage, memberSession.user.userName)).toBeVisible({ timeout: 10000 })

      await openTaskDetail(memberPage, 'todo', taskTitle)
      await memberPage.getByRole('button', { name: '關閉視窗' }).click()
      await expect(memberPage.getByRole('dialog', { name: '任務詳細資訊' })).toHaveCount(0)
      await expect(memberPage.getByRole('heading', { name: projectName })).toBeVisible()

      await memberPage.waitForTimeout(3500)

      await expect(getOnlineUserItem(ownerPage, memberSession.user.userName)).toBeVisible()
    } finally {
      await Promise.all([ownerContext.close(), memberContext.close()])
    }
  })

  test('成員切換到另一個 Project 後，舊 Project 在線名單應移除該成員，新的 Project 在線名單應顯示該成員', async ({ browser, request }, testInfo) => {
    const suffix = `${testInfo.workerIndex}-${testInfo.retry}-${Date.now()}`
    const firstProjectName = `Presence Source Project ${suffix}`
    const secondProjectName = `Presence Target Project ${suffix}`
    const {
      firstOwnerPage,
      secondOwnerPage,
      memberPage,
      firstOwnerContext,
      secondOwnerContext,
      memberContext,
      memberSession,
    } = await setupCrossProjectPresenceObservation(browser, request, firstProjectName, secondProjectName)

    try {
      await expect(getOnlineUserItem(firstOwnerPage, memberSession.user.userName)).toBeVisible({ timeout: 10000 })
      await expect(getOnlineUserItem(secondOwnerPage, memberSession.user.userName)).toHaveCount(0)

      await openProjectFromList(memberPage, secondProjectName)

      await expect(getOnlineUserItem(firstOwnerPage, memberSession.user.userName)).toHaveCount(0, { timeout: 10000 })
      await expect(getOnlineUserItem(secondOwnerPage, memberSession.user.userName)).toBeVisible({ timeout: 10000 })
    } finally {
      await Promise.all([firstOwnerContext.close(), secondOwnerContext.close(), memberContext.close()])
    }
  })
})