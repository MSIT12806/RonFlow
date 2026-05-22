import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  openProjectMembersPanel,
  setupProjectBoard,
  workflowColumns,
} from './support/ronflowTestHelpers'
import { registerAndEnterWorkspace } from './support/ronflowAuthTestHelpers'

const projectInvitationsRoute = '**/api/projects/*/invitations'
const projectMembersRoute = '**/api/projects/*/members'
const projectsRoute = '**/api/projects'
const projectBoardRoute = '**/api/projects/*/board'

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

    await expect.poll(() => postedPayload).toEqual({ invitee })
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

  test('邀請送出進行中時會鎖定輸入與按鈕，避免重複送出', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const invitee = `teammate-${Date.now()}@example.test`
    let invitationRequestCount = 0
    let releaseInvitationRequest: (() => void) | null = null
    const invitationRequestReleased = new Promise<void>((resolve) => {
      releaseInvitationRequest = resolve
    })

    await page.route(projectInvitationsRoute, async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: [] }),
        })
        return
      }

      if (route.request().method() !== 'POST') {
        await route.continue()
        return
      }

      invitationRequestCount += 1
      await invitationRequestReleased
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({ id: 'invitation-inflight-1', invitee }),
      })
    })

    await setupProjectBoard(page, projectName)
    await openProjectMembersPanel(page)

    const inviteeInput = page.getByLabel('邀請對象')
    const inviteButton = page.getByRole('button', { name: '邀請', exact: true })

    await inviteeInput.fill(invitee)
    await inviteButton.dblclick()

    await expect.poll(() => invitationRequestCount).toBe(1)
    await expect(inviteeInput).toBeDisabled()
    await expect(inviteButton).toBeDisabled()

    if (releaseInvitationRequest) {
      releaseInvitationRequest()
    }

    await expect(page.getByText(invitee, { exact: true })).toBeVisible()
  })

  test('嘗試邀請已是目前專案成員的使用者時，顯示重複邀請錯誤訊息', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const existingMember = `member-${Date.now()}@example.test`

    await page.route(projectInvitationsRoute, async (route) => {
      if (route.request().method() !== 'POST') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({
          errors: {
            invitee: ['該使用者已是目前專案成員'],
          },
        }),
      })
    })

    await setupProjectBoard(page, projectName)
    await openProjectMembersPanel(page)

    const inviteeInput = page.getByLabel('邀請對象')

    await inviteeInput.fill(existingMember)
    await page.getByRole('button', { name: '邀請', exact: true }).click()

    await expect(page.getByText('該使用者已是目前專案成員', { exact: true })).toBeVisible()
    await expect(inviteeInput).toHaveValue(existingMember)
  })

  test('邀請不存在的使用者時，顯示找不到可邀請使用者', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const unknownUser = `unknown-${Date.now()}@example.test`

    await page.route(projectInvitationsRoute, async (route) => {
      if (route.request().method() !== 'POST') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({
          errors: {
            invitee: ['找不到可邀請的使用者'],
          },
        }),
      })
    })

    await setupProjectBoard(page, projectName)
    await openProjectMembersPanel(page)

    const inviteeInput = page.getByLabel('邀請對象')

    await inviteeInput.fill(unknownUser)
    await page.getByRole('button', { name: '邀請', exact: true }).click()

    await expect(page.getByText('找不到可邀請的使用者', { exact: true })).toBeVisible()
    await expect(inviteeInput).toHaveValue(unknownUser)
  })

  test('專案成員面板載入失敗時，顯示 section-level 錯誤訊息而不是一般內容', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await page.route(projectMembersRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'members load failed' }),
      })
    })

    await page.route(projectInvitationsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'invitations load failed' }),
      })
    })

    await setupProjectBoard(page, projectName)
    await openProjectMembersPanel(page)

    await expect(page.getByTestId('base-error-state')).toContainText('無法載入專案成員資料，請稍後再試。')
    await expect(page.getByText('目前沒有待處理邀請', { exact: true })).toHaveCount(0)
  })

  test('成員查詢成功但 invitations 查詢失敗時，保留成員區塊並顯示待處理邀請錯誤', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const ownerName = `owner-${Date.now()}`

    await page.route(projectMembersRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [{ userName: ownerName, role: '專案擁有者' }],
        }),
      })
    })

    await page.route(projectInvitationsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'invitations load failed' }),
      })
    })

    await setupProjectBoard(page, projectName)
    await openProjectMembersPanel(page)

    await expect(page.getByText(ownerName, { exact: true })).toBeVisible()
    await expect(page.getByLabel('邀請對象')).toBeVisible()
    await expect(page.getByTestId('base-error-state')).toContainText('無法載入待處理邀請，請稍後再試。')
    await expect(page.getByText('目前沒有待處理邀請', { exact: true })).toHaveCount(0)
    await expect(page.getByText('無法載入專案成員資料，請稍後再試。', { exact: true })).toHaveCount(0)
  })

  test('non-owner 在專案看板上看不到成員面板入口，因此無法操作邀請', async ({ page }) => {
    const projectId = 'member-project-1'
    const projectName = `Member Project ${Date.now()}`

    await page.route(projectsRoute, async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [{
            id: projectId,
            name: projectName,
            updatedAt: '2026-05-20T09:00:00Z',
            role: '專案成員',
          }],
        }),
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

    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expect(page.getByRole('button', { name: '專案成員', exact: true })).toHaveCount(0)
    await expect(page.getByLabel('邀請對象')).toHaveCount(0)
  })
})