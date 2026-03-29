import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { apiFetch } from '@/api/client'

interface User {
  email: string
  isAdmin: boolean
  externalProvider: string | null
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const isLoading = ref(true)
  const isAuthenticated = computed(() => user.value !== null)
  const isAdmin = computed(() => user.value?.isAdmin ?? false)
  const isExternalAccount = computed(() => user.value?.externalProvider != null)

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
    await apiFetch('/identity/register', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    await login(email, password, false)
  }

  async function login(email: string, password: string, rememberMe: boolean) {
    const params = new URLSearchParams({
      useCookies: 'true',
      useSessionCookies: rememberMe ? 'false' : 'true',
    })
    await apiFetch(`/identity/login?${params}`, {
      method: 'POST',
      body: JSON.stringify({ email, password }),
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

  return {
    user,
    isLoading,
    isAuthenticated,
    isAdmin,
    isExternalAccount,
    fetchUser,
    register,
    login,
    logout,
    changeEmail,
    changePassword,
    forgotPassword,
    resetPassword,
  }
})
