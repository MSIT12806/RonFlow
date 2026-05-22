import { ronAuthClient } from '../auth/ronauthClient'
import type { AuthenticationResponse, PasswordLoginInput, RegisterUserInput, UserProfile } from '@ronauth/sdk'
export { RonAuthRequestError, RonAuthValidationError } from '@ronauth/sdk'
export type { AuthenticationResponse, PasswordLoginInput, RegisterUserInput, UserProfile } from '@ronauth/sdk'

export async function registerWithRonAuth(input: RegisterUserInput): Promise<AuthenticationResponse> {
  return ronAuthClient.register(input)
}

export async function loginWithRonAuth(input: PasswordLoginInput): Promise<AuthenticationResponse> {
  return ronAuthClient.login(input)
}

export async function bootstrapRonAuthSession(): Promise<AuthenticationResponse | null> {
  return ronAuthClient.bootstrap()
}

export async function refreshRonAuthSession(): Promise<AuthenticationResponse | null> {
  return ronAuthClient.refresh()
}

export async function getRonAuthCurrentUser(): Promise<UserProfile | null> {
  return ronAuthClient.getCurrentUser()
}

export async function logoutFromRonAuth(): Promise<void> {
  return ronAuthClient.logout()
}