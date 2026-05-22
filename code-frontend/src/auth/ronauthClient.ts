import { createMemoryAccessTokenStore, createRonAuthClient } from '@ronauth/sdk'

export const ronAuthAccessTokenStore = createMemoryAccessTokenStore()

export const ronAuthClient = createRonAuthClient({
  baseUrl: import.meta.env.VITE_RONAUTH_API_BASE_URL ?? '/ronauth-api/api/auth',
  tokenStore: ronAuthAccessTokenStore,
})