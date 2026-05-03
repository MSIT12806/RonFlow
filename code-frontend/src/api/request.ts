import { ApiRequestError, ApiValidationError } from './errors'
import type { ValidationErrorBag } from './types'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api'

export function apiPath(path: string) {
  return `${apiBaseUrl}${path}`
}

export async function request<T>(input: string, init?: RequestInit) {
  const response = await fetch(input, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
  })

  if (response.ok) {
    return (await response.json()) as T
  }

  if (response.status === 400) {
    const payload = (await response.json()) as { errors?: ValidationErrorBag }
    throw new ApiValidationError(payload.errors ?? {})
  }

  if (response.status === 404) {
    throw new ApiRequestError('resource not found', response.status)
  }

  throw new ApiRequestError(`request failed with status ${response.status}`, response.status)
}