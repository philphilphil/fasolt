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

  async function deleteAccount(password?: string, confirmIdentity?: string) {
    await apiFetch('/account', {
      method: 'DELETE',
      body: JSON.stringify({ password, confirmIdentity }),
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
    logout,
    changeEmail,
    changePassword,
    deleteAccount,
  }
})
