import { expect, test, type APIRequestContext, type Browser, type Page } from '@playwright/test'
import {
  acceptInvitationThroughApi,
  configureTestFaultsThroughApi,
  createProjectThroughApi,
  getInvitationInboxThroughApi,
  inviteProjectMemberThroughApi,
  registerRonFlowApiUser,
  rejectInvitationThroughApi,
} from './support/ronflowApiTestHelpers'
import { createRonFlowAuthUser, loginAndEnterWorkspace } from './support/ronflowAuthTestHelpers'
import { openInvitationInbox, openProjectFromList, openProjectMembersPanel } from './support/ronflowTestHelpers'

const staleInvitationMessage = '此邀請狀態已變更，已更新收件匣。'

async function setupPendingInvitation(request: APIRequestContext, projectName: string) {
  const ownerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))
  const memberSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('member'))
  const project = await createProjectThroughApi(request, ownerSession, projectName)

  await inviteProjectMemberThroughApi(request, ownerSession, project.id, memberSession.user.email)

  const invitation = (await getInvitationInboxThroughApi(request, memberSession)).find(
    (item) => item.projectName === projectName,
  )

  if (!invitation) {
    throw new Error(`Invitation for ${projectName} was not created.`)
  }

  return { ownerSession, memberSession, project, invitation }
}

async function loginMemberAndOpenInbox(page: Page, memberUser: { userName: string; password: string }) {
  await loginAndEnterWorkspace(page, memberUser)
  await openInvitationInbox(page)
}

async function expectInvitationVisible(page: Page, projectName: string) {
  await expect(page.getByTestId('invitation-item').filter({ hasText: projectName })).toBeVisible()
}

async function expectInvitationRemoved(page: Page, projectName: string) {
  await expect(page.getByTestId('invitation-item').filter({ hasText: projectName })).toHaveCount(0)
}

test.describe('RonFlow UI/UX 驗收規格 - Invitation Inbox Behavior', () => {
  test('使用者接受邀請後，邀請會從清單消失且專案出現在專案列表', async ({ page, request }) => {
    const projectName = `Invited Project ${Date.now()}`
    const { memberSession } = await setupPendingInvitation(request, projectName)

    await loginMemberAndOpenInbox(page, memberSession.user)
    await expectInvitationVisible(page, projectName)

    await page.getByRole('button', { name: '接受邀請', exact: true }).click()

    await expectInvitationRemoved(page, projectName)
    await expect(page.getByRole('button', { name: new RegExp(projectName) })).toBeVisible()
  })

  test('使用者接受邀請後，可以從專案列表再進入該專案看板', async ({ page, request }) => {
    const projectName = `Accepted Project ${Date.now()}`
    const { memberSession } = await setupPendingInvitation(request, projectName)

    await loginMemberAndOpenInbox(page, memberSession.user)
    await page.getByRole('button', { name: '接受邀請', exact: true }).click()

    await openProjectFromList(page, projectName)

    await expect(page.getByTestId('workflow-column-todo')).toContainText('待處理')
  })

  test('接受邀請成功後，專案列表應顯示「專案成員」角色', async ({ page, request }) => {
    const projectName = `Member Role Project ${Date.now()}`
    const { memberSession } = await setupPendingInvitation(request, projectName)

    await loginMemberAndOpenInbox(page, memberSession.user)
    await page.getByRole('button', { name: '接受邀請', exact: true }).click()

    await expect(page.getByRole('button', { name: new RegExp(projectName) })).toBeVisible()
    await expect(page.locator('.project-chip-role').filter({ hasText: '專案成員' })).toBeVisible()
  })

  test('使用者拒絕邀請後，邀請會從清單消失且不會取得專案存取權', async ({ page, request }) => {
    const projectName = `Rejected Project ${Date.now()}`
    const { memberSession } = await setupPendingInvitation(request, projectName)

    await loginMemberAndOpenInbox(page, memberSession.user)
    await expectInvitationVisible(page, projectName)

    await page.getByRole('button', { name: '拒絕邀請', exact: true }).click()

    await expectInvitationRemoved(page, projectName)
    await expect(page.getByRole('button', { name: projectName, exact: true })).toHaveCount(0)
  })

  test('接受邀請失敗時，邀請仍保留在清單中並顯示錯誤訊息', async ({ page, request }) => {
    const projectName = `Failed Invitation ${Date.now()}`
    const { memberSession } = await setupPendingInvitation(request, projectName)

    await configureTestFaultsThroughApi(request, memberSession, [{
      method: 'POST',
      pathPattern: '/api/invitations/*/accept',
      statusCode: 500,
      message: 'accept failed',
    }])

    await loginMemberAndOpenInbox(page, memberSession.user)
    await page.getByRole('button', { name: '接受邀請', exact: true }).click()

    await expectInvitationVisible(page, projectName)
    await expect(page.getByText('接受邀請失敗，請稍後再試。', { exact: true })).toBeVisible()
  })

  test('邀請已在其他地方被接受後再次接受時，會刷新清單並同步專案列表', async ({ page, request }) => {
    const projectName = `Stale Accepted Project ${Date.now()}`
    const { memberSession, invitation } = await setupPendingInvitation(request, projectName)

    await loginMemberAndOpenInbox(page, memberSession.user)
    await acceptInvitationThroughApi(request, memberSession, invitation.id)

    await page.getByRole('button', { name: '接受邀請', exact: true }).click()

    await expectInvitationRemoved(page, projectName)
    await expect(page.getByText(staleInvitationMessage, { exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: new RegExp(projectName) })).toBeVisible()
  })

  test('拒絕邀請失敗時，邀請仍保留在清單中並顯示錯誤訊息', async ({ page, request }) => {
    const projectName = `Reject Failed Invitation ${Date.now()}`
    const { memberSession } = await setupPendingInvitation(request, projectName)

    await configureTestFaultsThroughApi(request, memberSession, [{
      method: 'POST',
      pathPattern: '/api/invitations/*/reject',
      statusCode: 500,
      message: 'reject failed',
    }])

    await loginMemberAndOpenInbox(page, memberSession.user)
    await page.getByRole('button', { name: '拒絕邀請', exact: true }).click()

    await expectInvitationVisible(page, projectName)
    await expect(page.getByText('拒絕邀請失敗，請稍後再試。', { exact: true })).toBeVisible()
  })

  test('邀請已在其他地方被拒絕後再次拒絕時，會刷新清單並移除過期邀請', async ({ page, request }) => {
    const projectName = `Stale Rejected Project ${Date.now()}`
    const { memberSession, invitation } = await setupPendingInvitation(request, projectName)

    await loginMemberAndOpenInbox(page, memberSession.user)
    await rejectInvitationThroughApi(request, memberSession, invitation.id)

    await page.getByRole('button', { name: '拒絕邀請', exact: true }).click()

    await expectInvitationRemoved(page, projectName)
    await expect(page.getByText(staleInvitationMessage, { exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: projectName, exact: true })).toHaveCount(0)
  })

  test('接受邀請進行中時會鎖定動作按鈕，避免重複送出', async ({ page, request }) => {
    const projectName = `Accept Inflight Project ${Date.now()}`
    const { memberSession } = await setupPendingInvitation(request, projectName)

    await configureTestFaultsThroughApi(request, memberSession, [{
      method: 'POST',
      pathPattern: '/api/invitations/*/accept',
      statusCode: null,
      delayMs: 1200,
    }])

    await loginMemberAndOpenInbox(page, memberSession.user)

    const acceptButton = page.getByRole('button', { name: '接受邀請', exact: true })
    const rejectButton = page.getByRole('button', { name: '拒絕邀請', exact: true })

    await acceptButton.dblclick()

    await expect(acceptButton).toBeDisabled()
    await expect(rejectButton).toBeDisabled()
    await expectInvitationRemoved(page, projectName)
  })

  test('拒絕邀請進行中時會鎖定動作按鈕，避免重複送出', async ({ page, request }) => {
    const projectName = `Reject Inflight Project ${Date.now()}`
    const { memberSession } = await setupPendingInvitation(request, projectName)

    await configureTestFaultsThroughApi(request, memberSession, [{
      method: 'POST',
      pathPattern: '/api/invitations/*/reject',
      statusCode: null,
      delayMs: 1200,
    }])

    await loginMemberAndOpenInbox(page, memberSession.user)

    const acceptButton = page.getByRole('button', { name: '接受邀請', exact: true })
    const rejectButton = page.getByRole('button', { name: '拒絕邀請', exact: true })

    await rejectButton.dblclick()

    await expect(acceptButton).toBeDisabled()
    await expect(rejectButton).toBeDisabled()
    await expectInvitationRemoved(page, projectName)
  })

  test('邀請收件匣載入失敗時，顯示 section-level 錯誤訊息而不是空清單', async ({ page, request }) => {
    const memberSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('member'))

    await configureTestFaultsThroughApi(request, memberSession, [{
      method: 'GET',
      pathPattern: '/api/invitations',
      statusCode: 500,
      message: 'load failed',
    }])

    await loginMemberAndOpenInbox(page, memberSession.user)

    await expect(page.getByTestId('base-error-state')).toContainText('無法載入邀請收件匣，請稍後再試。')
    await expect(page.getByText('目前沒有待處理邀請', { exact: true })).toHaveCount(0)
  })

  test('invitation conflict 後若 inbox refresh 也失敗，應顯示載入錯誤且不顯示同步成功訊息', async ({ page, request }) => {
    const projectName = `Conflict Refresh Failed ${Date.now()}`
    const { memberSession, invitation } = await setupPendingInvitation(request, projectName)

    await loginMemberAndOpenInbox(page, memberSession.user)
    await acceptInvitationThroughApi(request, memberSession, invitation.id)
    await configureTestFaultsThroughApi(request, memberSession, [{
      method: 'GET',
      pathPattern: '/api/invitations',
      statusCode: 500,
      message: 'refresh failed',
    }])

    await page.getByRole('button', { name: '接受邀請', exact: true }).click()

    await expect(page.getByTestId('base-error-state')).toContainText('無法載入邀請收件匣，請稍後再試。')
    await expect(page.getByTestId('invitation-sync-message')).toHaveCount(0)
    await expectInvitationRemoved(page, projectName)
  })

  test('A 邀請 B，B 接受後可進入專案，A 重新整理後可在 members panel 看到 B', async ({ browser, request }) => {
    const projectName = `Cross User Project ${Date.now()}`
    const ownerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))
    const memberSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('member'))
    const project = await createProjectThroughApi(request, ownerSession, projectName)

    const ownerContext = await browser.newContext()
    const memberContext = await browser.newContext()
    const ownerPage = await ownerContext.newPage()
    const memberPage = await memberContext.newPage()

    await loginAndEnterWorkspace(ownerPage, ownerSession.user)
    await openProjectFromList(ownerPage, projectName)
    await openProjectMembersPanel(ownerPage)

    await ownerPage.getByLabel('邀請對象').fill(memberSession.user.email)
    await ownerPage.getByRole('button', { name: '邀請', exact: true }).click()
    await expect(ownerPage.getByText(memberSession.user.email, { exact: true })).toBeVisible()

    await loginAndEnterWorkspace(memberPage, memberSession.user)
    await openInvitationInbox(memberPage)
    await expectInvitationVisible(memberPage, projectName)
    await memberPage.getByRole('button', { name: '接受邀請', exact: true }).click()

    await expect(memberPage.locator('.project-chip-role').filter({ hasText: '專案成員' })).toBeVisible()
    await openProjectFromList(memberPage, projectName)

    await ownerPage.reload()
    await openProjectFromList(ownerPage, projectName)
    await openProjectMembersPanel(ownerPage)
    await expect(ownerPage.getByRole('listitem').filter({ hasText: memberSession.user.userName })).toBeVisible()

    await ownerContext.close()
    await memberContext.close()
  })
})