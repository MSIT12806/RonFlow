import { expect, test, type Locator } from '@playwright/test'
import {
  createScenarioData,
  openTaskDetail,
  setupTaskBoard,
} from './support/ronflowTestHelpers'

const reminderCreateRoute = '**/api/projects/*/tasks/*/reminders'
const reminderDeleteRoute = '**/api/projects/*/tasks/*/reminders/*'

function getRemindersSection(detailDialog: Locator) {
  return detailDialog.getByTestId('task-reminders-section')
}

function getReminderItem(detailDialog: Locator, reminderDescription: string) {
  return getRemindersSection(detailDialog)
    .getByTestId('task-reminder-item')
    .filter({ hasText: reminderDescription })
    .first()
}

async function mockPushNotifications(page: Parameters<typeof test>[0]['page']) {
  await page.addInitScript(() => {
    let notificationPermission = 'default'
    const subscriptionJson = {
      endpoint: 'https://push.example.test/subscriptions/device-1',
      keys: {
        p256dh: 'p256dh-key',
        auth: 'auth-key',
      },
    }

    class MockPushManager {
      async getSubscription() {
        return null
      }

      async subscribe() {
        return {
          toJSON() {
            return subscriptionJson
          },
        }
      }
    }

    Object.defineProperty(window, 'PushManager', {
      configurable: true,
      value: MockPushManager,
    })

    const serviceWorkerRegistration = {
      pushManager: new MockPushManager(),
    }

    Object.defineProperty(navigator, 'serviceWorker', {
      configurable: true,
      value: {
        register: async () => serviceWorkerRegistration,
      },
    })

    if (!('Notification' in window)) {
      class MockNotification {}
      Object.defineProperty(window, 'Notification', {
        configurable: true,
        value: MockNotification,
      })
    }

    Object.defineProperty(window.Notification, 'permission', {
      configurable: true,
      get: () => notificationPermission,
    })

    window.Notification.requestPermission = async () => {
      notificationPermission = 'granted'
      return 'granted'
    }
  })
}

async function addReminder(detailDialog: Locator, reminderDateTime: string, reminderDescription: string) {
  await expect(getRemindersSection(detailDialog)).toContainText('提醒')
  await detailDialog.getByLabel('提醒時間').fill(reminderDateTime)
  await detailDialog.getByLabel('提醒說明').fill(reminderDescription)
  await detailDialog.getByRole('button', { name: '新增提醒', exact: true }).click()
}

test.describe('RonFlow UI/UX 驗收規格 - Task Reminders Behavior', () => {
  test('使用者可以在 Task Detail Drawer 新增一筆提醒', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const reminderDateTime = '2026-05-20T09:00'
    const reminderDescription = '提醒確認欄位狀態'

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await addReminder(detailDialog, reminderDateTime, reminderDescription)

    const reminderItem = getReminderItem(detailDialog, reminderDescription)

    await expect(reminderItem).toContainText('2026-05-20 09:00')
    await expect(reminderItem).toContainText(reminderDescription)
  })

  test('使用者可以為同一個 task 新增多筆提醒', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const firstReminderDateTime = '2026-05-20T09:00'
    const firstReminderDescription = '提醒確認欄位狀態'
    const secondReminderDateTime = '2026-05-21T15:00'
    const secondReminderDescription = '提醒追蹤審查結果'

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })
    const remindersSection = getRemindersSection(detailDialog)

    await addReminder(detailDialog, firstReminderDateTime, firstReminderDescription)
    await addReminder(detailDialog, secondReminderDateTime, secondReminderDescription)

    await expect(remindersSection.getByTestId('task-reminder-item')).toHaveCount(2)
    await expect(getReminderItem(detailDialog, firstReminderDescription)).toContainText('2026-05-20 09:00')
    await expect(getReminderItem(detailDialog, firstReminderDescription)).toContainText(firstReminderDescription)
    await expect(getReminderItem(detailDialog, secondReminderDescription)).toContainText('2026-05-21 15:00')
    await expect(getReminderItem(detailDialog, secondReminderDescription)).toContainText(secondReminderDescription)
  })

  test('Reminder DateTime 為必填', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const reminderDescription = '提醒確認欄位狀態'

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await expect(getRemindersSection(detailDialog)).toContainText('提醒')
    await detailDialog.getByLabel('提醒說明').fill(reminderDescription)
    await detailDialog.getByRole('button', { name: '新增提醒', exact: true }).click()

    await expect(detailDialog).toBeVisible()
    await expect(detailDialog.getByText('提醒時間為必填欄位', { exact: true })).toBeVisible()
  })

  test('使用者可以啟用此裝置的提醒通知', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    let registeredSubscription: Record<string, unknown> | null = null

    await mockPushNotifications(page)

    await page.route('**/api/notifications/push/public-key', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ publicKey: 'BCV2ZXRlc3QtcHVibGljLWtleQ' }),
      })
    })

    await page.route('**/api/notifications/push/subscriptions', async (route) => {
      registeredSubscription = route.request().postDataJSON() as Record<string, unknown>
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({}),
      })
    })

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })
    const deliveryStatus = getRemindersSection(detailDialog).getByTestId('reminder-delivery-status')

    await expect(deliveryStatus).toContainText('提醒可能無法送達，請先啟用此裝置的提醒通知。')
    await detailDialog.getByRole('button', { name: '啟用提醒通知', exact: true }).click()

    await expect(deliveryStatus).toContainText('此裝置已啟用提醒通知，提醒會以通知方式送達。')
    expect(registeredSubscription).toEqual({
      endpoint: 'https://push.example.test/subscriptions/device-1',
      keys: {
        p256dh: 'p256dh-key',
        auth: 'auth-key',
      },
    })
  })

  test('使用者可以刪除尚未觸發的提醒', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const reminderDateTime = '2026-05-20T09:00'
    const reminderDescription = '提醒確認欄位狀態'

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await addReminder(detailDialog, reminderDateTime, reminderDescription)
    await expect(getReminderItem(detailDialog, reminderDescription)).toBeVisible()

    await getReminderItem(detailDialog, reminderDescription)
      .getByRole('button', { name: '刪除提醒', exact: true })
      .click()

    await expect(getReminderItem(detailDialog, reminderDescription)).toHaveCount(0)
  })

  test('提醒建立失敗時，drawer 保持開啟且保留輸入內容', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const reminderDateTime = '2026-05-20T09:00'
    const reminderDescription = '提醒確認欄位狀態'

    await page.route(reminderCreateRoute, async (route) => {
      if (route.request().method() !== 'POST') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'create reminder failed' }),
      })
    })

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await addReminder(detailDialog, reminderDateTime, reminderDescription)

    await expect(detailDialog).toBeVisible()
    await expect(detailDialog).toContainText('新增提醒失敗，請稍後再試。')
    await expect(detailDialog.getByLabel('提醒時間')).toHaveValue(reminderDateTime)
    await expect(detailDialog.getByLabel('提醒說明')).toHaveValue(reminderDescription)
  })

  test('提醒刪除失敗時，原提醒仍留在畫面上且有錯誤回饋', async ({ page }, testInfo) => {
    const { projectName, taskTitle } = createScenarioData(testInfo)
    const reminderDateTime = '2026-05-20T09:00'
    const reminderDescription = '提醒確認欄位狀態'

    await setupTaskBoard(page, projectName, taskTitle)
    await openTaskDetail(page, 'todo', taskTitle)

    const detailDialog = page.getByRole('dialog', { name: '任務詳細資訊' })

    await addReminder(detailDialog, reminderDateTime, reminderDescription)

    await page.route(reminderDeleteRoute, async (route) => {
      if (route.request().method() !== 'DELETE') {
        await route.continue()
        return
      }

      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'delete reminder failed' }),
      })
    })

    await getReminderItem(detailDialog, reminderDescription)
      .getByRole('button', { name: '刪除提醒', exact: true })
      .click()

    await expect(getReminderItem(detailDialog, reminderDescription)).toContainText('2026-05-20 09:00')
    await expect(getReminderItem(detailDialog, reminderDescription)).toContainText(reminderDescription)
    await expect(detailDialog).toContainText('刪除提醒失敗，請稍後再試。')
  })
})
