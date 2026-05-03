import type { ValidationErrorBag } from './types'

export class ApiValidationError extends Error {
  readonly errors: ValidationErrorBag

  constructor(errors: ValidationErrorBag) {
    super('API validation failed')
    this.errors = errors
  }
}

export class ApiRequestError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}