import { expect, test } from '@playwright/test'
import {
  openCreateProjectModal,
} from './support/ronflowTestHelpers'

test.describe('RonFlow UI/UX 驗收規格 - Project List Screen', () => {
  test('專案列表首屏顯示建立專案入口與空狀態', async ({ page }) => {
    await page.route('**/api/projects', async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: [] }),
        })
        return
      }

      await route.continue()
    })

    await page.goto('/')

    await expect(page).toHaveTitle(/RonFlow/)
    await expect(page.getByRole('heading', { name: '專案列表' })).toBeVisible()
    await expect(page.getByRole('button', { name: '建立專案' })).toBeVisible()
    await expect(page.getByRole('button', { name: '建立專案' })).toBeInViewport()
    await expect(page.getByText('尚未建立任何專案')).toBeVisible()
  })

  test('可以從專案列表開啟建立專案對話框', async ({ page }) => {
    await page.goto('/')

    await openCreateProjectModal(page)
  })

  test('專案列表載入失敗時顯示 page-level 錯誤而不是空白清單', async ({ page }) => {
    await page.route('**/api/projects', async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ message: 'load failed' }),
        })
        return
      }

      await route.continue()
    })

    await page.goto('/')

    await expect(page.getByText('無法載入專案列表，請確認後端 API 已啟動。')).toBeVisible()
    await expect(page.getByText('尚未建立任何專案')).toHaveCount(0)
  })

  test('建立專案對話框開啟後焦點直接落在專案名稱欄位，且只有單一輸入欄位', async ({ page }) => {
    await page.goto('/')
    await openCreateProjectModal(page)

    const dialog = page.getByRole('dialog', { name: '建立專案' })
    const projectNameInput = dialog.getByLabel('專案名稱')

    await expect(dialog.getByRole('textbox')).toHaveCount(1)
    await expect(projectNameInput).toBeFocused()
  })
})
