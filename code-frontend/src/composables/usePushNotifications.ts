import { computed, onMounted, ref } from 'vue'
import { PushNotificationCommandService } from '../application'

type ReminderDeliveryState = 'unsupported' | 'permission-required' | 'denied' | 'ready' | 'error'

const pushNotificationCommandService = new PushNotificationCommandService()

export function usePushNotifications() {
  const deliveryState = ref<ReminderDeliveryState>('permission-required')
  const isEnabling = ref(false)

  const reminderDeliveryStatusMessage = computed(() => {
    switch (deliveryState.value) {
      case 'ready':
        return '此裝置已啟用提醒通知，提醒會以通知方式送達。'
      case 'unsupported':
        return '提醒可能無法送達，請確認目前裝置支援通知與推播功能。'
      case 'denied':
        return '提醒可能無法送達，請確認通知權限與推播訂閱狀態。'
      case 'error':
        return '提醒通知啟用失敗，請稍後再試。'
      default:
        return '提醒可能無法送達，請先啟用此裝置的提醒通知。'
    }
  })

  const canEnableReminderDelivery = computed(() =>
    deliveryState.value === 'permission-required' || deliveryState.value === 'error',
  )

  onMounted(() => {
    void initializePushNotifications()
  })

  async function initializePushNotifications() {
    if (!supportsPushNotifications()) {
      deliveryState.value = 'unsupported'
      return
    }

    if (window.Notification.permission === 'denied') {
      deliveryState.value = 'denied'
      return
    }

    if (window.Notification.permission !== 'granted') {
      deliveryState.value = 'permission-required'
      return
    }

    const isReady = await ensureSubscriptionRegistered()
    deliveryState.value = isReady ? 'ready' : 'error'
  }

  async function enableReminderDelivery() {
    if (isEnabling.value) {
      return
    }

    if (!supportsPushNotifications()) {
      deliveryState.value = 'unsupported'
      return
    }

    isEnabling.value = true

    try {
      const permission = window.Notification.permission === 'granted'
        ? 'granted'
        : await window.Notification.requestPermission()

      if (permission !== 'granted') {
        deliveryState.value = permission === 'denied' ? 'denied' : 'permission-required'
        return
      }

      const isReady = await ensureSubscriptionRegistered()
      deliveryState.value = isReady ? 'ready' : 'error'
    } catch {
      deliveryState.value = 'error'
    } finally {
      isEnabling.value = false
    }
  }

  return {
    reminderDeliveryStatusMessage,
    canEnableReminderDelivery,
    isEnablingReminderDelivery: computed(() => isEnabling.value),
    enableReminderDelivery,
  }
}

function supportsPushNotifications() {
  return typeof window !== 'undefined'
    && typeof navigator !== 'undefined'
    && 'Notification' in window
    && 'serviceWorker' in navigator
    && 'PushManager' in window
}

async function ensureSubscriptionRegistered() {
  const serviceWorkerRegistration = await navigator.serviceWorker.register('/service-worker.js')
  let subscription = await serviceWorkerRegistration.pushManager.getSubscription()

  if (!subscription) {
    const publicKey = await pushNotificationCommandService.getPublicKey()
    subscription = await serviceWorkerRegistration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(publicKey),
    })
  }

  const serializedSubscription = subscription.toJSON()
  const endpoint = serializedSubscription.endpoint
  const p256dh = serializedSubscription.keys?.p256dh
  const auth = serializedSubscription.keys?.auth

  if (!endpoint || !p256dh || !auth) {
    return false
  }

  await pushNotificationCommandService.registerSubscription({
    endpoint,
    keys: {
      p256dh,
      auth,
    },
  })

  return true
}

function urlBase64ToUint8Array(base64String: string) {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4)
  const normalizedBase64 = `${base64String}${padding}`
    .replace(/-/g, '+')
    .replace(/_/g, '/')
  const rawData = window.atob(normalizedBase64)
  const outputArray = new Uint8Array(rawData.length)

  for (let index = 0; index < rawData.length; index += 1) {
    outputArray[index] = rawData.charCodeAt(index)
  }

  return outputArray
}