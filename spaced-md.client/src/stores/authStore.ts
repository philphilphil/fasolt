// stores/authStore.ts
import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface UserInfo {
  email: string
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<UserInfo | null>(null)
  const isAuthenticated = ref(false)

  function setUser(data: UserInfo | null) {
    user.value = data
    isAuthenticated.value = !!data
  }

  return { user, isAuthenticated, setUser }
})
