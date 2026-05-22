import { expect, test } from '@playwright/test'
import { openInvitationInbox } from './support/ronflowTestHelpers'
import { registerAndEnterWorkspace } from './support/ronflowAuthTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Invitation Inbox Screen', () => {
  test('使用者可以從專案列表進入邀請收件匣', async ({ page }) => {
    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    await expect(page.getByRole('heading', { name: '邀請收件匣' })).toBeVisible()
  })

  test('邀請收件匣會顯示邀請處理所需的主要操作區域', async ({ page }) => {
    await registerAndEnterWorkspace(page)
    await openInvitationInbox(page)

    await expect(page.getByText('接受邀請', { exact: true })).toBeVisible()
    await expect(page.getByText('拒絕邀請', { exact: true })).toBeVisible()
  })
})