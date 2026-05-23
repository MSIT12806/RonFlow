import { expect, test } from '@playwright/test'
import {
  createProject,
  createScenarioData,
  openCreateProjectModal,
  openInvitationInbox,
} from './support/ronflowTestHelpers'
import { createRonFlowAuthUser, loginAndEnterWorkspace, registerAndEnterWorkspace } from './support/ronflowAuthTestHelpers'
import { configureTestFaultsThroughApi, registerRonFlowApiUser } from './support/ronflowApiTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Project List Screen', () => {
  test('未登入時首頁顯示登入與註冊入口', async ({ page }) => {
    await page.goto('/')

    await expect(page).toHaveTitle(/RonFlow/)
    await expect(page.getByRole('heading', { name: '登入 RonFlow' })).toBeVisible()
    await expect(page.getByRole('button', { name: '註冊' })).toBeVisible()
    await expect(page.getByRole('heading', { name: '專案列表' })).toHaveCount(0)
  })

  test('專案列表首屏顯示建立專案入口、邀請收件匣入口與空狀態', async ({ page }) => {
    await registerAndEnterWorkspace(page)

    await expect(page).toHaveTitle(/RonFlow/)
    await expect(page.getByRole('heading', { name: '專案列表' })).toBeVisible()
    await expect(page.getByRole('button', { name: '建立專案' })).toBeVisible()
    await expect(page.getByRole('button', { name: '建立專案' })).toBeInViewport()
    await expect(page.getByText('邀請收件匣', { exact: true })).toBeVisible()
    await expect(page.getByText('尚未建立任何專案')).toBeVisible()
  })

  test('可以從專案列表開啟建立專案對話框', async ({ page }) => {
    await registerAndEnterWorkspace(page)

    await openCreateProjectModal(page)
  })

  test('專案列表載入失敗時顯示 page-level 錯誤而不是空白清單', async ({ page, request }) => {
    const userSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))

    await configureTestFaultsThroughApi(request, userSession, [{
      method: 'GET',
      pathPattern: '/api/projects',
      statusCode: 500,
      message: 'load failed',
    }])

    await loginAndEnterWorkspace(page, userSession.user)

    await expect(page.getByText('無法載入專案列表，請確認後端 API 已啟動。')).toBeVisible()
    await expect(page.getByText('尚未建立任何專案')).toHaveCount(0)
  })

  test('建立專案對話框開啟後焦點直接落在專案名稱欄位，且只有單一輸入欄位', async ({ page }) => {
    await registerAndEnterWorkspace(page)
    await openCreateProjectModal(page)

    const dialog = page.getByRole('dialog', { name: '建立專案' })
    const projectNameInput = dialog.getByLabel('專案名稱')

    await expect(dialog.getByRole('textbox')).toHaveCount(1)
    await expect(projectNameInput).toBeFocused()
  })

  test('專案列表會顯示使用者在專案中的角色', async ({ page }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)

    await registerAndEnterWorkspace(page)
    await openCreateProjectModal(page)
    await createProject(page, projectName)
    await page.goto('/')

    await expect(page.getByRole('heading', { name: '專案列表' })).toBeVisible()
    await expect(page.getByRole('button', { name: new RegExp(projectName) })).toBeVisible()
    await expect(page.locator('.project-chip-role').filter({ hasText: '專案擁有者' })).toBeVisible()
  })

  test('可以從專案列表開啟邀請收件匣', async ({ page }) => {
    await registerAndEnterWorkspace(page)

    await openInvitationInbox(page)
  })
})
