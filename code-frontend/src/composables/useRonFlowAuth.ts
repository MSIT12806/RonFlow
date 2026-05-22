import { computed, ref } from 'vue'
import {
  bootstrapRonAuthSession,
  getRonAuthCurrentUser,
  loginWithRonAuth,
  logoutFromRonAuth,
  registerWithRonAuth,
  RonAuthRequestError,
  RonAuthValidationError,
  type PasswordLoginInput,
  type RegisterUserInput,
  type UserProfile,
} from '../api/ronauth'

export function useRonFlowAuth() {
  const user = ref<UserProfile | null>(null)
  const isInitializing = ref(false)
  const isSubmitting = ref(false)
  const errorMessage = ref('')
  const validationErrors = ref<Record<string, string[]>>({})

  const isAuthenticated = computed(() => user.value !== null)

  async function initialize() {
    isInitializing.value = true
    clearErrors()

    try {
      const response = await bootstrapRonAuthSession()
      user.value = response?.user ?? null
      return user.value !== null
    } catch {
      user.value = null
      errorMessage.value = '無法還原登入狀態，請重新登入。'
      return false
    } finally {
      isInitializing.value = false
    }
  }

  async function login(input: PasswordLoginInput) {
    return executeAuthAction(async () => loginWithRonAuth(input), {
      defaultErrorMessage: '登入失敗，請稍後再試。',
      secondFactorMessage: '目前 RonFlow 尚未提供第二因子登入畫面。',
    })
  }

  async function register(input: RegisterUserInput) {
    return executeAuthAction(async () => registerWithRonAuth(input), {
      defaultErrorMessage: '註冊失敗，請稍後再試。',
    })
  }

  async function loadCurrentUser() {
    try {
      user.value = await getRonAuthCurrentUser()
      return user.value
    } catch {
      errorMessage.value = '無法載入目前使用者資訊。'
      return null
    }
  }

  async function logout() {
    try {
      await logoutFromRonAuth()
    } finally {
      user.value = null
      clearErrors()
    }
  }

  function clearErrors() {
    errorMessage.value = ''
    validationErrors.value = {}
  }

  async function executeAuthAction(
    action: () => Promise<{ status: string; message: string; user: UserProfile | null }>,
    options: { defaultErrorMessage: string; secondFactorMessage?: string },
  ) {
    isSubmitting.value = true
    clearErrors()

    try {
      const response = await action()
      if (response.status === 'Success' && response.user) {
        user.value = response.user
        return true
      }

      user.value = null
      errorMessage.value = response.status === 'RequiresSecondFactor'
        ? options.secondFactorMessage ?? response.message
        : response.message || options.defaultErrorMessage
      return false
    } catch (error) {
      user.value = null
      if (error instanceof RonAuthValidationError) {
        validationErrors.value = error.errors
        errorMessage.value = '請修正表單內容後再送出。'
      } else if (error instanceof RonAuthRequestError) {
        errorMessage.value = options.defaultErrorMessage
      } else {
        errorMessage.value = options.defaultErrorMessage
      }

      return false
    } finally {
      isSubmitting.value = false
    }
  }

  return {
    user,
    isAuthenticated,
    isInitializing,
    isSubmitting,
    errorMessage,
    validationErrors,
    initialize,
    login,
    register,
    loadCurrentUser,
    logout,
    clearErrors,
  }
}