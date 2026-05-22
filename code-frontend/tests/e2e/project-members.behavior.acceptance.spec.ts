import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  openProjectMembersPanel,
  setupProjectBoard,
} from './support/ronflowTestHelpers'

const projectInvitationsRoute = '**/api/projects/*/invitations'

test.describe('RonFlow UI/UX 驗收規格 - Project Members Behavior', () => {
  test('邀請對象為必填', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await setupProjectBoard(page, projectName)
    await openProjectMembersPanel(page)

    await page.getByRole('button', { name: '邀請', exact: true }).click()

    await expect(page.getByText('邀請對象為必填欄位', { exact: true })).toBeVisible()
  })

  test('Project Owner 可以送出邀請並在待處理清單看到結果', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const invitee = `teammate-${Date.now()}@example.test`
    const pendingInvitations: Array<{ id: string; invitee: string }> = []
    let postedPayload: unknown = null

    await page.route(projectInvitationsRoute, async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: pendingInvitations }),
        })
        return
      }

      if (route.request().method() === 'POST') {
        postedPayload = route.request().postDataJSON()
        pendingInvitations.push({
          id: 'invitation-1',
          invitee,
        })

        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'invitation-1', invitee }),
        })
        return
      }

      await route.continue()
    })

    await setupProjectBoard(page, projectName)
    await openProjectMembersPanel(page)

    const inviteeInput = page.getByLabel('邀請對象')

    await inviteeInput.fill(invitee)
    await page.getByRole('button', { name: '邀請', exact: true }).click()

    expect(postedPayload).toEqual({ invitee })
    await expect(page.getByText(invitee, { exact: true })).toBeVisible()
    await expect(inviteeInput).toHaveValue('')
  })

  test('邀請失敗時保留輸入內容並顯示錯誤訊息', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const invitee = `teammate-${Date.now()}@example.test`
    let releaseInvitationFailure: (() => void) | null = null
    const invitationFailureReleased = new Promise<void>((resolve) => {
      releaseInvitationFailure = resolve
    })

    await page.route(projectInvitationsRoute, async (route) => {
      if (route.request().method() !== 'POST') {
        await route.continue()
        return
      }

      await invitationFailureReleased
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'invite failed' }),
      })
    })

    await setupProjectBoard(page, projectName)
    await openProjectMembersPanel(page)

    const inviteeInput = page.getByLabel('邀請對象')
    const inviteButton = page.getByRole('button', { name: '邀請', exact: true })

    await inviteeInput.fill(invitee)
    await inviteButton.click()

    await expect(inviteeInput).toBeDisabled()
    await expect(inviteButton).toBeDisabled()

    if (releaseInvitationFailure) {
      releaseInvitationFailure()
    }

    await expect(page.getByText('邀請成員失敗，請稍後再試。', { exact: true })).toBeVisible()
    await expect(inviteeInput).toHaveValue(invitee)
  })
})