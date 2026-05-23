import { expect, type Page } from '@playwright/test'

export type RonFlowAuthUser = {
  userName: string
  email: string
  password: string
}

export function createRonFlowAuthUser(label = 'ronflow-e2e'): RonFlowAuthUser {
  const suffix = `${Date.now()}-${Math.floor(Math.random() * 100000)}`

  return {
    userName: `${label}-${suffix}`,
    email: `${label}-${suffix}@example.test`,
    password: 'Admin123!',
  }
}

export async function registerAndEnterWorkspace(page: Page, user = createRonFlowAuthUser()) {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: '登入 RonFlow' })).toBeVisible()

  await page.getByRole('button', { name: '註冊' }).click()
  const form = page.locator('form')

  await form.getByLabel('帳號').fill(user.userName)
  await form.getByLabel('電子郵件').fill(user.email)
  await form.getByLabel('密碼').fill(user.password)
  await form.getByRole('button', { name: '建立帳號並登入' }).click()

  await expect(page.getByRole('button', { name: '登出' })).toBeVisible()
  return user
}

export async function loginAndEnterWorkspace(page: Page, user: RonFlowAuthUser) {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: '登入 RonFlow' })).toBeVisible()

  await page.getByRole('button', { name: '登入' }).first().click()
  const form = page.locator('form')

  await form.getByLabel('帳號').fill(user.userName)
  await form.getByLabel('密碼').fill(user.password)
  await form.getByRole('button', { name: '登入', exact: true }).click()

  await expect(page.getByRole('button', { name: '登出' })).toBeVisible()
}