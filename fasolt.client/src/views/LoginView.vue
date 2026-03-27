<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'

const router = useRouter()
const auth = useAuthStore()

const email = ref('')
const password = ref('')
const rememberMe = ref(false)
const error = ref('')
const loading = ref(false)

async function handleSubmit() {
  error.value = ''
  loading.value = true
  try {
    await auth.login(email.value, password.value, rememberMe.value)
    router.push('/')
  } catch (e) {
    if (isApiError(e) && e.status === 401) {
      error.value = 'Invalid email or password.'
    } else {
      error.value = 'Something went wrong. Please try again.'
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <Card class="border-border/60">
    <CardHeader>
      <CardTitle class="text-center text-base">Log in</CardTitle>
    </CardHeader>
    <CardContent>
      <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
        <div v-if="error" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">
          {{ error }}
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="email" class="text-xs font-medium">Email</label>
          <Input id="email" v-model="email" type="email" required autocomplete="username" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="password" class="text-xs font-medium">Password</label>
          <Input id="password" v-model="password" type="password" required autocomplete="current-password" />
        </div>
        <label class="flex items-center gap-2 text-xs">
          <input v-model="rememberMe" type="checkbox" class="rounded" />
          Remember me
        </label>
        <Button type="submit" class="w-full" :disabled="loading">
          {{ loading ? 'Logging in\u2026' : 'Log in' }}
        </Button>
        <div class="flex flex-col items-center gap-1 text-xs">
          <RouterLink to="/register" class="text-accent hover:underline">Create an account</RouterLink>
          <RouterLink to="/forgot-password" class="text-muted-foreground hover:underline">Forgot password?</RouterLink>
        </div>
      </form>
    </CardContent>
  </Card>
</template>
