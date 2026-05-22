<template>
  <section class="auth-panel">
    <div class="auth-panel-copy">
      <p class="eyebrow">RonAuth SDK</p>
      <h2 class="auth-panel-title">登入 RonFlow</h2>
      <p class="auth-panel-subtitle">
        這一版先用 RonAuth SDK 串接最小的註冊、登入與 session restore 流程。
      </p>
    </div>

    <div class="auth-mode-toggle">
      <button
        type="button"
        class="auth-mode-button"
        :class="{ 'auth-mode-button-active': mode === 'login' }"
        @click="mode = 'login'"
      >
        登入
      </button>
      <button
        type="button"
        class="auth-mode-button"
        :class="{ 'auth-mode-button-active': mode === 'register' }"
        @click="mode = 'register'"
      >
        註冊
      </button>
    </div>

    <p v-if="isInitializing" class="auth-inline-message">正在還原登入狀態...</p>
    <p v-else-if="errorMessage" class="auth-inline-error">{{ errorMessage }}</p>

    <form v-if="mode === 'login'" class="auth-form" @submit.prevent="onLoginSubmit">
      <label>
        帳號
        <input v-model="loginForm.userName" autocomplete="username" required>
      </label>

      <label>
        密碼
        <input v-model="loginForm.password" type="password" autocomplete="current-password" required>
      </label>

      <button type="submit" class="primary-button" :disabled="isSubmitting || isInitializing">
        {{ isSubmitting ? '登入中...' : '登入' }}
      </button>
    </form>

    <form v-else class="auth-form" @submit.prevent="onRegisterSubmit">
      <label>
        帳號
        <input v-model="registerForm.userName" autocomplete="username" required>
        <span v-if="validationErrors.userName?.[0]" class="auth-field-error">{{ validationErrors.userName[0] }}</span>
      </label>

      <label>
        電子郵件
        <input v-model="registerForm.email" type="email" autocomplete="email" required>
        <span v-if="validationErrors.email?.[0]" class="auth-field-error">{{ validationErrors.email[0] }}</span>
      </label>

      <label>
        密碼
        <input v-model="registerForm.password" type="password" autocomplete="new-password" required>
        <span v-if="validationErrors.password?.[0]" class="auth-field-error">{{ validationErrors.password[0] }}</span>
      </label>

      <button type="submit" class="primary-button" :disabled="isSubmitting || isInitializing">
        {{ isSubmitting ? '建立中...' : '建立帳號並登入' }}
      </button>
    </form>
  </section>
</template>

<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import type { PasswordLoginInput, RegisterUserInput } from '../api/ronauth'

const props = defineProps<{
  isInitializing: boolean
  isSubmitting: boolean
  errorMessage: string
  validationErrors: Record<string, string[]>
}>()

const emit = defineEmits<{
  login: [payload: PasswordLoginInput]
  register: [payload: RegisterUserInput]
}>()

const mode = ref<'login' | 'register'>('login')
const loginForm = reactive<PasswordLoginInput>({ userName: '', password: '' })
const registerForm = reactive<RegisterUserInput>({ userName: '', email: '', password: '' })

watch(() => props.errorMessage, () => {
  if (props.validationErrors.userName || props.validationErrors.email || props.validationErrors.password) {
    mode.value = 'register'
  }
})

function onLoginSubmit() {
  emit('login', { ...loginForm })
}

function onRegisterSubmit() {
  emit('register', { ...registerForm })
}
</script>