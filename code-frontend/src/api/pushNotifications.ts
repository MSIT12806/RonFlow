import { apiPath, request } from './request'
import type { PushNotificationPublicKeyResponse } from './types'

export type PushSubscriptionRegistration = {
  endpoint: string
  keys: {
    p256dh: string
    auth: string
  }
}

export async function getPushNotificationPublicKey() {
  return request<PushNotificationPublicKeyResponse>(apiPath('/notifications/push/public-key'))
}

export async function registerPushSubscription(payload: PushSubscriptionRegistration) {
  return request<void>(apiPath('/notifications/push/subscriptions'), {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}