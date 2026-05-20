import {
  getPushNotificationPublicKey,
  registerPushSubscription,
  type PushSubscriptionRegistration,
} from '../api/ronflowApi'

export class PushNotificationCommandService {
  async getPublicKey(): Promise<string> {
    const response = await getPushNotificationPublicKey()
    return response.publicKey
  }

  async registerSubscription(subscription: PushSubscriptionRegistration): Promise<void> {
    await registerPushSubscription(subscription)
  }
}