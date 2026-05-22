import { expect, test } from '@playwright/test'
import {
  createProject,
  openCreateProjectModal,
  openInvitationInbox,
  openProjectMembersPanel,
  workflowColumns,
} from './support/ronflowTestHelpers'
import { createRonFlowAuthUser, registerAndEnterWorkspace } from './support/ronflowAuthTestHelpers'

const projectsRoute = '**/api/projects'
const invitationInboxRoute = '**/api/invitations'
const acceptInvitationRoute = '**/api/invitations/*/accept'
const rejectInvitationRoute = '**/api/invitations/*/reject'
const projectBoardRoute = '**/api/projects/*/board'
const projectMembersRoute = '**/api/projects/*/members'
const projectInvitationsRoute = '**/api/projects/*/invitations'
const staleInvitationMessage = '此邀請狀態已變更，已更新收件匣。'

function createBoardResponse(projectId: string, projectName: string) {
  return {
    projectId,
    projectName,
    columns: workflowColumns.map((column) => ({
      stateKey: column.key,
      label: column.label,
      isInitialState: column.key === 'todo',
      isCompletedState: column.key === 'done',
      emptyStateMessage: '目前沒有任務',
      tasks: [],
    })),
  }
}

test.describe('RonFlow UI/UX 驗收規格 - Invitation Inbox Behavior', () => {
  test('使用者接受邀請後，邀請會從清單消失且專案出現在專案列表', async ({ page }) => {
    const projectName = `Invited Project ${Date.now()}`
    const invitationId = 'invitation-accept-1'
    const pendingInvitations = [{ id: invitationId, projectName, inviterName: 'project-owner' }]
    const projects: Array<{ id: string; name: string; updatedAt: string }> = []

    await page.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: projects }),
      })
    })

    await page.route(invitationInboxRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: pendingInvitations }),
      })
    })

    await page.route(acceptInvitationRoute, async (route) => {
      pendingInvitations.splice(0, pendingInvitations.length)
      projects.push({
        id: 'project-accepted-1',
        name: projectName,
        updatedAt: '2026-05-20T09:00:00Z',
      })

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({}),
      })
    })

    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    await expect(page.getByText(projectName, { exact: true })).toBeVisible()
    await page.getByRole('button', { name: '接受邀請', exact: true }).click()

    await expect(page.getByTestId('invitation-item').filter({ hasText: projectName })).toHaveCount(0)
    await expect(page.getByRole('button', { name: new RegExp(projectName) })).toBeVisible()
  })

  test('使用者接受邀請後，可以從專案列表再進入該專案看板', async ({ page }) => {
    const projectName = `Accepted Project ${Date.now()}`
    const projectId = 'project-accepted-flow-1'
    const invitationId = 'invitation-accept-flow-1'
    const pendingInvitations = [{ id: invitationId, projectName, inviterName: 'project-owner' }]
    const projects: Array<{ id: string; name: string; updatedAt: string }> = []

    await page.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: projects }),
      })
    })

    await page.route(invitationInboxRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: pendingInvitations }),
      })
    })

    await page.route(acceptInvitationRoute, async (route) => {
      pendingInvitations.splice(0, pendingInvitations.length)
      projects.push({
        id: projectId,
        name: projectName,
        updatedAt: '2026-05-20T09:00:00Z',
      })

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({}),
      })
    })

    await page.route(projectBoardRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          projectId,
          projectName,
          columns: workflowColumns.map((column) => ({
            stateKey: column.key,
            label: column.label,
            isInitialState: column.key === 'todo',
            isCompletedState: column.key === 'done',
            emptyStateMessage: '目前沒有任務',
            tasks: [],
          })),
        }),
      })
    })

    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    await page.getByRole('button', { name: '接受邀請', exact: true }).click()
    await expect(page.getByRole('button', { name: new RegExp(projectName) })).toBeVisible()

    await page.getByRole('button', { name: new RegExp(projectName) }).click()

    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expect(page.getByTestId('workflow-column-todo')).toContainText('待處理')
  })

  test('接受邀請成功後，專案列表應顯示「專案成員」角色', async ({ page }) => {
    const projectName = `Member Role Project ${Date.now()}`
    const invitationId = 'invitation-member-role-1'
    const pendingInvitations = [{ id: invitationId, projectName, inviterName: 'project-owner' }]
    const projects: Array<{ id: string; name: string; updatedAt: string; role: string }> = []

    await page.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: projects }),
      })
    })

    await page.route(invitationInboxRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: pendingInvitations }),
      })
    })

    await page.route(acceptInvitationRoute, async (route) => {
      pendingInvitations.splice(0, pendingInvitations.length)
      projects.push({
        id: 'project-member-role-1',
        name: projectName,
        updatedAt: '2026-05-20T09:00:00Z',
        role: '專案成員',
      })

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({}),
      })
    })

    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    await page.getByRole('button', { name: '接受邀請', exact: true }).click()

    await expect(page.getByRole('button', { name: new RegExp(projectName) })).toBeVisible()
    await expect(page.locator('.project-chip-role').filter({ hasText: '專案成員' })).toBeVisible()
  })

  test('使用者拒絕邀請後，邀請會從清單消失且不會取得專案存取權', async ({ page }) => {
    const projectName = `Rejected Project ${Date.now()}`
    const invitationId = 'invitation-reject-1'
    const pendingInvitations = [{ id: invitationId, projectName, inviterName: 'project-owner' }]

    await page.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [] }),
      })
    })

    await page.route(invitationInboxRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: pendingInvitations }),
      })
    })

    await page.route(rejectInvitationRoute, async (route) => {
      pendingInvitations.splice(0, pendingInvitations.length)

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({}),
      })
    })

    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    await expect(page.getByText(projectName, { exact: true })).toBeVisible()
    await page.getByRole('button', { name: '拒絕邀請', exact: true }).click()

    await expect(page.getByText(projectName, { exact: true })).toHaveCount(0)
    await expect(page.getByRole('button', { name: projectName, exact: true })).toHaveCount(0)
  })

  test('接受邀請失敗時，邀請仍保留在清單中並顯示錯誤訊息', async ({ page }) => {
    const projectName = `Failed Invitation ${Date.now()}`
    const pendingInvitations = [{ id: 'invitation-failed-1', projectName, inviterName: 'project-owner' }]

    await page.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [] }),
      })
    })

    await page.route(invitationInboxRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: pendingInvitations }),
      })
    })

    await page.route(acceptInvitationRoute, async (route) => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'accept failed' }),
      })
    })

    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    await page.getByRole('button', { name: '接受邀請', exact: true }).click()

    await expect(page.getByText(projectName, { exact: true })).toBeVisible()
    await expect(page.getByText('接受邀請失敗，請稍後再試。', { exact: true })).toBeVisible()
  })

  test('邀請已在其他地方被接受後再次接受時，會刷新清單並同步專案列表', async ({ page }) => {
    const projectName = `Stale Accepted Project ${Date.now()}`
    const pendingInvitations = [{ id: 'invitation-stale-accept-1', projectName, inviterName: 'project-owner' }]
    const projects: Array<{ id: string; name: string; updatedAt: string; role: string }> = []

    await page.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: projects }),
      })
    })

    await page.route(invitationInboxRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: pendingInvitations }),
      })
    })

    await page.route(acceptInvitationRoute, async (route) => {
      pendingInvitations.splice(0, pendingInvitations.length)
      projects.splice(0, projects.length, {
        id: 'project-stale-accept-1',
        name: projectName,
        updatedAt: '2026-05-20T09:00:00Z',
        role: '專案成員',
      })

      await route.fulfill({
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'already handled' }),
      })
    })

    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    await page.getByRole('button', { name: '接受邀請', exact: true }).click()

    await expect(page.getByTestId('invitation-item').filter({ hasText: projectName })).toHaveCount(0)
    await expect(page.getByText(staleInvitationMessage, { exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: new RegExp(projectName) })).toBeVisible()
  })

  test('拒絕邀請失敗時，邀請仍保留在清單中並顯示錯誤訊息', async ({ page }) => {
    const projectName = `Reject Failed Invitation ${Date.now()}`
    const pendingInvitations = [{ id: 'invitation-reject-failed-1', projectName, inviterName: 'project-owner' }]

    await page.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [] }),
      })
    })

    await page.route(invitationInboxRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: pendingInvitations }),
      })
    })

    await page.route(rejectInvitationRoute, async (route) => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'reject failed' }),
      })
    })

    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    await page.getByRole('button', { name: '拒絕邀請', exact: true }).click()

    await expect(page.getByText(projectName, { exact: true })).toBeVisible()
    await expect(page.getByText('拒絕邀請失敗，請稍後再試。', { exact: true })).toBeVisible()
  })

  test('邀請已在其他地方被拒絕後再次拒絕時，會刷新清單並移除過期邀請', async ({ page }) => {
    const projectName = `Stale Rejected Project ${Date.now()}`
    const pendingInvitations = [{ id: 'invitation-stale-reject-1', projectName, inviterName: 'project-owner' }]

    await page.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [] }),
      })
    })

    await page.route(invitationInboxRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: pendingInvitations }),
      })
    })

    await page.route(rejectInvitationRoute, async (route) => {
      pendingInvitations.splice(0, pendingInvitations.length)

      await route.fulfill({
        status: 404,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'already handled' }),
      })
    })

    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    await page.getByRole('button', { name: '拒絕邀請', exact: true }).click()

    await expect(page.getByTestId('invitation-item').filter({ hasText: projectName })).toHaveCount(0)
    await expect(page.getByText(staleInvitationMessage, { exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: projectName, exact: true })).toHaveCount(0)
  })

  test('接受邀請進行中時會鎖定動作按鈕，避免重複送出', async ({ page }) => {
    const projectName = `Accept Inflight Project ${Date.now()}`
    const pendingInvitations = [{ id: 'invitation-accept-inflight-1', projectName, inviterName: 'project-owner' }]
    let acceptRequestCount = 0
    let releaseAcceptRequest: (() => void) | null = null
    const acceptRequestReleased = new Promise<void>((resolve) => {
      releaseAcceptRequest = resolve
    })

    await page.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [] }),
      })
    })

    await page.route(invitationInboxRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: pendingInvitations }),
      })
    })

    await page.route(acceptInvitationRoute, async (route) => {
      acceptRequestCount += 1
      await acceptRequestReleased
      pendingInvitations.splice(0, pendingInvitations.length)

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({}),
      })
    })

    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    const acceptButton = page.getByRole('button', { name: '接受邀請', exact: true })
    const rejectButton = page.getByRole('button', { name: '拒絕邀請', exact: true })

    await acceptButton.dblclick()

    await expect.poll(() => acceptRequestCount).toBe(1)
    await expect(acceptButton).toBeDisabled()
    await expect(rejectButton).toBeDisabled()

    if (releaseAcceptRequest) {
      releaseAcceptRequest()
    }

    await expect(page.getByTestId('invitation-item').filter({ hasText: projectName })).toHaveCount(0)
  })

  test('拒絕邀請進行中時會鎖定動作按鈕，避免重複送出', async ({ page }) => {
    const projectName = `Reject Inflight Project ${Date.now()}`
    const pendingInvitations = [{ id: 'invitation-reject-inflight-1', projectName, inviterName: 'project-owner' }]
    let rejectRequestCount = 0
    let releaseRejectRequest: (() => void) | null = null
    const rejectRequestReleased = new Promise<void>((resolve) => {
      releaseRejectRequest = resolve
    })

    await page.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [] }),
      })
    })

    await page.route(invitationInboxRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: pendingInvitations }),
      })
    })

    await page.route(rejectInvitationRoute, async (route) => {
      rejectRequestCount += 1
      await rejectRequestReleased
      pendingInvitations.splice(0, pendingInvitations.length)

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({}),
      })
    })

    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    const acceptButton = page.getByRole('button', { name: '接受邀請', exact: true })
    const rejectButton = page.getByRole('button', { name: '拒絕邀請', exact: true })

    await rejectButton.dblclick()

    await expect.poll(() => rejectRequestCount).toBe(1)
    await expect(acceptButton).toBeDisabled()
    await expect(rejectButton).toBeDisabled()

    if (releaseRejectRequest) {
      releaseRejectRequest()
    }

    await expect(page.getByTestId('invitation-item').filter({ hasText: projectName })).toHaveCount(0)
  })

  test('邀請收件匣載入失敗時，顯示 section-level 錯誤訊息而不是空清單', async ({ page }) => {
    await page.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [] }),
      })
    })

    await page.route(invitationInboxRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'load failed' }),
      })
    })

    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    await expect(page.getByTestId('base-error-state')).toContainText('無法載入邀請收件匣，請稍後再試。')
    await expect(page.getByText('目前沒有待處理邀請', { exact: true })).toHaveCount(0)
  })

  test('invitation conflict 後若 inbox refresh 也失敗，應顯示載入錯誤且不顯示同步成功訊息', async ({ page }) => {
    const projectName = `Conflict Refresh Failed ${Date.now()}`
    const pendingInvitations = [{ id: 'invitation-conflict-refresh-failed-1', projectName, inviterName: 'project-owner' }]
    let inboxLoadCount = 0

    await page.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [] }),
      })
    })

    await page.route(invitationInboxRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      inboxLoadCount += 1

      if (inboxLoadCount === 1) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: pendingInvitations }),
        })
        return
      }

      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'refresh failed' }),
      })
    })

    await page.route(acceptInvitationRoute, async (route) => {
      await route.fulfill({
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'already handled' }),
      })
    })

    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    await page.getByRole('button', { name: '接受邀請', exact: true }).click()

    await expect(page.getByTestId('base-error-state')).toContainText('無法載入邀請收件匣，請稍後再試。')
    await expect(page.getByTestId('invitation-sync-message')).toHaveCount(0)
    await expect(page.getByTestId('invitation-item').filter({ hasText: projectName })).toHaveCount(0)
  })

  test('A 邀請 B，B 接受後可進入專案，A 重新整理後可在 members panel 看到 B', async ({ browser }) => {
    const ownerUser = createRonFlowAuthUser('owner')
    const memberUser = createRonFlowAuthUser('member')
    const projectId = 'cross-user-project-1'
    const projectName = `Cross User Project ${Date.now()}`
    const updatedAt = '2026-05-20T09:00:00Z'

    const sharedState = {
      ownerProjects: [] as Array<{ id: string; name: string; updatedAt: string; role: string }>,
      memberProjects: [] as Array<{ id: string; name: string; updatedAt: string; role: string }>,
      ownerPendingInvitations: [] as Array<{ id: string; invitee: string }>,
      memberInbox: [] as Array<{ id: string; projectName: string; inviterName: string }>,
      members: [{ userName: ownerUser.userName, role: '專案擁有者' }] as Array<{ userName: string; role: string }>,
    }

    const ownerContext = await browser.newContext()
    const memberContext = await browser.newContext()
    const ownerPage = await ownerContext.newPage()
    const memberPage = await memberContext.newPage()

    await ownerPage.route(projectsRoute, async (route) => {
      if (route.request().method() === 'POST') {
        sharedState.ownerProjects.splice(0, sharedState.ownerProjects.length, {
          id: projectId,
          name: projectName,
          updatedAt,
          role: '專案擁有者',
        })

        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: projectId,
            name: projectName,
            updatedAt,
            workflowStates: [],
          }),
        })
        return
      }

      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: sharedState.ownerProjects }),
        })
        return
      }

      await route.continue()
    })

    await ownerPage.route(projectBoardRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(createBoardResponse(projectId, projectName)),
      })
    })

    await ownerPage.route(projectMembersRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: sharedState.members }),
      })
    })

    await ownerPage.route(projectInvitationsRoute, async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: sharedState.ownerPendingInvitations }),
        })
        return
      }

      if (route.request().method() === 'POST') {
        const payload = route.request().postDataJSON() as { invitee: string }
        sharedState.ownerPendingInvitations.push({ id: 'shared-invitation-1', invitee: payload.invitee })
        sharedState.memberInbox.push({
          id: 'shared-invitation-1',
          projectName,
          inviterName: ownerUser.userName,
        })

        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'shared-invitation-1', invitee: payload.invitee }),
        })
        return
      }

      await route.continue()
    })

    await memberPage.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: sharedState.memberProjects }),
      })
    })

    await memberPage.route(invitationInboxRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: sharedState.memberInbox }),
      })
    })

    await memberPage.route(acceptInvitationRoute, async (route) => {
      sharedState.memberInbox.splice(0, sharedState.memberInbox.length)
      sharedState.ownerPendingInvitations.splice(0, sharedState.ownerPendingInvitations.length)
      sharedState.memberProjects.splice(0, sharedState.memberProjects.length, {
        id: projectId,
        name: projectName,
        updatedAt,
        role: '專案成員',
      })
      sharedState.members.push({ userName: memberUser.userName, role: '專案成員' })

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({}),
      })
    })

    await memberPage.route(rejectInvitationRoute, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({}),
      })
    })

    await memberPage.route(projectBoardRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(createBoardResponse(projectId, projectName)),
      })
    })

    await registerAndEnterWorkspace(ownerPage, ownerUser)
    await openCreateProjectModal(ownerPage)
    await createProject(ownerPage, projectName)
    await openProjectMembersPanel(ownerPage)

    await ownerPage.getByLabel('邀請對象').fill(memberUser.email)
    await ownerPage.getByRole('button', { name: '邀請', exact: true }).click()
    await expect(ownerPage.getByText(memberUser.email, { exact: true })).toBeVisible()

    await registerAndEnterWorkspace(memberPage, memberUser)
    await openInvitationInbox(memberPage)
    await expect(memberPage.getByText(projectName, { exact: true })).toBeVisible()
    await memberPage.getByRole('button', { name: '接受邀請', exact: true }).click()

    await expect(memberPage.locator('.project-chip-role').filter({ hasText: '專案成員' })).toBeVisible()
    await memberPage.getByRole('button', { name: new RegExp(projectName) }).click()
    await expect(memberPage.getByRole('heading', { name: projectName })).toBeVisible()

    await ownerPage.reload()
    await expect(ownerPage.getByRole('heading', { name: projectName })).toBeVisible()
    await openProjectMembersPanel(ownerPage)
    await expect(ownerPage.getByText(memberUser.userName, { exact: true })).toBeVisible()

    await ownerContext.close()
    await memberContext.close()
  })
})