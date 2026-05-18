import { expect, test } from '@playwright/test'
import {
  createScenarioData,
  createTaskTitle,
  openTaskDetail,
  setupTaskBoard,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Task Detail', () => {
  test('任務詳細資訊顯示基本資訊，未完成任務不顯示完成時間', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog).toBeVisible()
    await expect(detailDialog.getByRole('heading', { name: '任務詳細資訊' })).toBeVisible()
    await expect(detailDialog.getByLabel('任務標題')).toHaveValue(taskTitle)
    await expect(detailDialog.getByText('待處理', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已建立任務', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('完成時間', { exact: true })).toHaveCount(0)
  })

  test('任務詳細資訊載入中時在 drawer 內顯示 shared loading state', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await page.route('**/api/projects/*/tasks/*', async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await new Promise((resolve) => setTimeout(resolve, 300))
      await route.continue()
    })

    await setupTaskBoard(page, projectName, taskTitle)
    await page.getByTestId('workflow-column-todo').getByText(taskTitle, { exact: true }).click()

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog).toBeVisible()
    await expect(detailDialog.getByTestId('base-loading-state')).toContainText('正在載入任務詳細資訊...')
    await expect(detailDialog.getByRole('heading', { name: taskTitle })).toBeVisible()
  })

  test('任務詳細資訊載入失敗時在 drawer 內顯示 shared error state', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await page.route('**/api/projects/*/tasks/*', async (route) => {
      if (route.request().method() !== 'GET') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'detail failed' }),
      })
    })

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog).toBeVisible()
    await expect(detailDialog.getByTestId('base-error-state')).toContainText('無法載入任務詳細資訊，請重新整理後再試。')
    await expect(page.getByText('無法載入任務詳細資訊，請重新整理後再試。')).toHaveCount(1)
  })

  test('重新整理後仍可看到已建立的專案與任務', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)

    await setupTaskBoard(page, projectName, taskTitle)

    await page.evaluate(() => {
      window.localStorage.clear()
      window.sessionStorage.clear()
    })
    await page.context().clearCookies()
    await page.reload()

    await page.getByRole('button', { name: projectName }).click()
    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expect(page.getByTestId('workflow-column-todo')).toContainText(taskTitle)
  })

  test('使用者可以在 Drawer 修改任務標題、描述與到期日，並看到最新資料與活動紀錄', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const updatedTaskTitle = createTaskTitle(testInfo, 'Updated Task Title')
    const updatedDescription = '支援在 Drawer 直接編輯任務內容與活動紀錄'
    const updatedDueDate = '2026-05-20'

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await detailDialog.getByLabel('任務標題').fill(updatedTaskTitle)
    await detailDialog.getByLabel('任務描述').fill(updatedDescription)
    await detailDialog.getByLabel('到期日').fill(updatedDueDate)
    await detailDialog.getByRole('button', { name: '儲存變更' }).click()

    await expect(detailDialog.getByLabel('任務標題')).toHaveValue(updatedTaskTitle)
    await expect(detailDialog.getByLabel('任務描述')).toHaveValue(updatedDescription)
    await expect(detailDialog.getByLabel('到期日')).toHaveValue(updatedDueDate)
    await expect(detailDialog.getByText('已更新任務標題', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已更新任務描述', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已設定到期日為 2026/05/20', { exact: true })).toBeVisible()
    await expect(page.getByTestId('workflow-column-todo')).toContainText(updatedTaskTitle)
    await expect(page.getByTestId('workflow-column-todo')).not.toContainText(taskTitle)
  })
})
