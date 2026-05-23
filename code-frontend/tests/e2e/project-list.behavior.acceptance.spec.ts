import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  openCreateProjectModal,
} from './support/ronflowTestHelpers'
import { createRonFlowAuthUser, loginAndEnterWorkspace, registerAndEnterWorkspace } from './support/ronflowAuthTestHelpers'
import { configureTestFaultsThroughApi, registerRonFlowApiUser } from './support/ronflowApiTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Project List Behavior', () => {
  test('拒絕空白的專案名稱', async ({ page }) => {
    await registerAndEnterWorkspace(page)
    await openCreateProjectModal(page)

    const dialog = page.getByRole('dialog', { name: '建立專案' })

    await dialog.getByRole('button', { name: '建立', exact: true }).click()

    await expect(page.getByText('專案名稱為必填欄位')).toBeVisible()
    await expect(dialog).toBeVisible()
  })

  test('建立專案失敗時保持對話框開啟並保留輸入內容', async ({ page, request }, testInfo) => {
    const { projectName } = createScenarioData(testInfo)
    const userSession = await registerRonFlowApiUser(request, createRonFlowAuthUser('owner'))

    await configureTestFaultsThroughApi(request, userSession, [{
      method: 'POST',
      pathPattern: '/api/projects',
      statusCode: 500,
      delayMs: 1200,
      message: 'create failed',
    }])

    await loginAndEnterWorkspace(page, userSession.user)
    await openCreateProjectModal(page)

    const dialog = page.getByRole('dialog', { name: '建立專案' })
    const projectNameInput = dialog.getByLabel('專案名稱')
    const submitButton = dialog.getByRole('button', { name: '建立', exact: true })
    const cancelButton = dialog.getByRole('button', { name: '取消' })
    const closeButton = dialog.getByRole('button', { name: '關閉視窗' })

    await projectNameInput.fill(projectName)
    await submitButton.click()

    await expect(projectNameInput).toBeDisabled()
    await expect(submitButton).toBeDisabled()
    await expect(cancelButton).toBeDisabled()
    await expect(closeButton).toBeDisabled()

    await expect(page.getByText('建立專案失敗，請稍後再試。')).toBeVisible()
    await expect(dialog).toBeVisible()
    await expect(projectNameInput).toHaveValue(projectName)
  })
})
