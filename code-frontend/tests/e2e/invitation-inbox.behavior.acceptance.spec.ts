import { expect, test } from '@playwright/test'
import { openInvitationInbox } from './support/ronflowTestHelpers'
import { registerAndEnterWorkspace } from './support/ronflowAuthTestHelpers'

const projectsRoute = '**/api/projects'
const invitationInboxRoute = '**/api/invitations'
const acceptInvitationRoute = '**/api/invitations/*/accept'
const rejectInvitationRoute = '**/api/invitations/*/reject'

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

    await expect(page.getByText(projectName, { exact: true })).toHaveCount(0)
    await expect(page.getByRole('button', { name: new RegExp(projectName) })).toBeVisible()
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
})