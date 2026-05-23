import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import {
  acceptInvitationThroughApi,
  configureTestFaultsThroughApi,
  createProjectThroughApi,
  getInvitationInboxThroughApi,
  getProjectInvitationsThroughApi,
  inviteProjectMemberThroughApi,
  registerRonFlowApiUser,
} from './support/ronflowApiTestHelpers'
import { createRonFlowAuthUser, loginAndEnterWorkspace } from './support/ronflowAuthTestHelpers'
import { createScenarioData, openProjectFromList, openProjectMembersPanel } from './support/ronflowTestHelpers'

async function loginOwnerAndOpenMembersPanel(page: Page, request: APIRequestContext, projectName: string) {
  const ownerSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))
  const project = await createProjectThroughApi(request, ownerSession, projectName)

  await loginAndEnterWorkspace(page, ownerSession.user)
  await openProjectFromList(page, projectName)
  await openProjectMembersPanel(page)

  return { ownerSession, project }
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

    await expect(page.getByRole('listitem').filter({ hasText: ownerSession.user.userName })).toBeVisible()
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
})