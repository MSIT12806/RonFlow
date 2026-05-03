import { expect, test } from '@playwright/test'

const workflowColumns = [
  { key: 'todo', label: '待處理' },
  { key: 'active', label: '進行中' },
  { key: 'review', label: '審查中' },
  { key: 'done', label: '已完成' },
]
const projectName = 'RonFlow Project'
const taskTitle = 'Build Kanban Board'

async function openCreateProjectModal(page: Parameters<typeof test>[0]['page']) {
  await page.getByRole('button', { name: '建立專案' }).click()
  await expect(page.getByRole('dialog', { name: '建立專案' })).toBeVisible()
}

async function createProject(page: Parameters<typeof test>[0]['page']) {
  const dialog = page.getByRole('dialog', { name: '建立專案' })

  await dialog.getByLabel('專案名稱').fill(projectName)
  await dialog.getByRole('button', { name: '建立', exact: true }).click()

  await expect(dialog).not.toBeVisible()
  await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
}

async function openCreateTaskModal(page: Parameters<typeof test>[0]['page']) {
  await page.getByRole('button', { name: '建立任務' }).click()
  await expect(page.getByRole('dialog', { name: '建立任務' })).toBeVisible()
}

async function createTask(page: Parameters<typeof test>[0]['page']) {
  const dialog = page.getByRole('dialog', { name: '建立任務' })

  await dialog.getByLabel('任務標題').fill(taskTitle)
  await dialog.getByRole('button', { name: '建立', exact: true }).click()

  await expect(dialog).not.toBeVisible()
}

test.describe('RonFlow 核心流程驗收規格', () => {
  test('顯示專案列表進入點與空清單狀態', async ({ page }) => {
    await page.goto('/')

    await expect(page).toHaveTitle(/RonFlow/)
    await expect(page.getByRole('heading', { name: '專案列表' })).toBeVisible()
    await expect(page.getByText('尚未建立任何專案')).toBeVisible()
    await expect(page.getByRole('button', { name: '建立專案' })).toBeVisible()
  })

  test('可以從專案列表開啟建立專案對話框', async ({ page }) => {
    await page.goto('/')

    await openCreateProjectModal(page)
  })

  test('拒絕空白的專案名稱', async ({ page }) => {
    await page.goto('/')

    await openCreateProjectModal(page)

    const dialog = page.getByRole('dialog', { name: '建立專案' })
    await dialog.getByRole('button', { name: '建立', exact: true }).click()

    await expect(page.getByText('專案名稱為必填欄位')).toBeVisible()
    await expect(dialog).toBeVisible()
  })

  test('建立專案後顯示專案看板與預設欄位', async ({ page }) => {
    await page.goto('/')

    await openCreateProjectModal(page)
    await createProject(page)

    for (const column of workflowColumns) {
      await expect(page.getByRole('heading', { name: column.label })).toBeVisible()
    }
  })

  test('專案看板的空欄位顯示預設訊息', async ({ page }) => {
    await page.goto('/')

    await openCreateProjectModal(page)
    await createProject(page)

    for (const column of workflowColumns) {
      const boardColumn = page.getByTestId(`workflow-column-${column.key}`)
      await expect(boardColumn).toContainText('目前沒有任務')
    }
  })

  test('拒絕空白的任務標題', async ({ page }) => {
    await page.goto('/')

    await openCreateProjectModal(page)
    await createProject(page)

    await openCreateTaskModal(page)

    const dialog = page.getByRole('dialog', { name: '建立任務' })
    await dialog.getByRole('button', { name: '建立', exact: true }).click()

    await expect(page.getByText('任務標題為必填欄位')).toBeVisible()
    await expect(dialog).toBeVisible()
  })

  test('使用者可以建立任務並顯示在待處理欄位', async ({ page }) => {
    await page.goto('/')

    await openCreateProjectModal(page)
    await createProject(page)

    await openCreateTaskModal(page)
    await createTask(page)

    const initialColumn = page.getByTestId('workflow-column-todo')
    await expect(initialColumn).toContainText(taskTitle)
    await expect(initialColumn).not.toContainText('目前沒有任務')
  })

  test('可以從看板開啟任務詳細資訊', async ({ page }) => {
    await page.goto('/')

    await openCreateProjectModal(page)
    await createProject(page)

    await openCreateTaskModal(page)
    await createTask(page)

    await page.getByTestId('workflow-column-todo').getByText(taskTitle, { exact: true }).click()

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(detailDialog).toBeVisible()
    await expect(detailDialog.getByRole('heading', { name: taskTitle })).toBeVisible()
    await expect(detailDialog.getByText('待處理', { exact: true })).toBeVisible()
    await expect(detailDialog.getByText('已建立任務', { exact: true })).toBeVisible()
  })

  test('重新整理後仍可看到已建立的專案與任務', async ({ page }) => {
    await page.goto('/')

    await openCreateProjectModal(page)
    await createProject(page)

    await openCreateTaskModal(page)
    await createTask(page)

    await page.reload()

    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await expect(page.getByTestId('workflow-column-todo')).toContainText(taskTitle)
  })
})