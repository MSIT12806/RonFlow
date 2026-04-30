import { expect, test } from '@playwright/test'

test('renders RonFlow frontend foundation home page', async ({ page }) => {
  await page.goto('/')

  await expect(page).toHaveTitle(/RonFlow/)
  await expect(page.getByTestId('hero-title')).toContainText('把專案流程')
  await expect(page.getByTestId('primary-cta')).toBeVisible()
  await expect(page.getByTestId('stack-chip')).toHaveCount(6)
  await expect(page.getByText('Foundation status')).toBeVisible()
})