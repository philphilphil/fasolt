import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { apiFetch } from '@/api/client'

interface User {
  email: string
  isAdmin: boolean
  emailConfirmed: boolean
  externalProvider: string | null
  displayName: string | null
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const isLoading = ref(true)
  const isAuthenticated = computed(() => user.value !== null)
  const isAdmin = computed(() => user.value?.isAdmin ?? false)
  const isExternalAccount = computed(() => user.value?.externalProvider != null)
  const isEmailConfirmed = computed(() => user.value?.emailConfirmed ?? false)

  async function fetchUser() {
    try {
      user.value = await apiFetch<User>('/account/me')
    } catch {
      user.value = null
    } finally {
      isLoading.value = false
    }
  }

  async function register(email: string, password: string) {
    await apiFetch('/account/register', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    await fetchUser()
  }

  async function login(email: string, password: string, rememberMe: boolean) {
    await apiFetch('/account/login', {
      method: 'POST',
      body: JSON.stringify({ email, password, rememberMe }),
    })
    await fetchUser()
  }

  async function logout() {
    try {
      await apiFetch('/account/logout', {
        method: 'POST',
      })
    } finally {
      user.value = null
    }
  }

  async function changeEmail(newEmail: string, currentPassword: string) {
    const result = await apiFetch<User>('/account/email', {
      method: 'PUT',
      body: JSON.stringify({ newEmail, currentPassword }),
    })
    user.value = result
    return result
  }

  async function changePassword(currentPassword: string, newPassword: string) {
    await apiFetch('/account/password', {
      method: 'PUT',
      body: JSON.stringify({ currentPassword, newPassword }),
    })
  }

  async function forgotPassword(email: string) {
    await apiFetch('/account/forgot-password', {
      method: 'POST',
      body: JSON.stringify({ email }),
    })
  }

  async function resetPassword(email: string, token: string, newPassword: string) {
    await apiFetch('/account/reset-password', {
      method: 'POST',
      body: JSON.stringify({ email, token, newPassword }),
    })
  }

  async function resendVerification() {
    await apiFetch('/account/resend-verification', { method: 'POST' })
  }

  async function exportData() {
    const response = await fetch('/api/account/export', {
      credentials: 'include',
    })
    if (!response.ok) throw new Error('Export failed')
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = response.headers.get('content-disposition')?.match(/filename="?(.+?)"?$/)?.[1] ?? 'fasolt-export.json'
    a.click()
    URL.revokeObjectURL(url)
  }

  async function deleteAccount(password?: string, confirmEmail?: string) {
    await apiFetch('/account', {
      method: 'DELETE',
      body: JSON.stringify({ password, confirmEmail }),
    })
    user.value = null
  }

  return {
    user,
    isLoading,
    isAuthenticated,
    isAdmin,
    isExternalAccount,
    isEmailConfirmed,
    fetchUser,
    register,
    login,
    logout,
    changeEmail,
    changePassword,
    forgotPassword,
    resetPassword,
    resendVerification,
    exportData,
    deleteAccount,
  }
})
