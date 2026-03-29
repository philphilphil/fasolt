<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

const email = ref('')
const password = ref('')
const rememberMe = ref(false)
const error = ref('')
const loading = ref(false)

const gitHubErrors: Record<string, string> = {
  email_exists: 'An account with this email already exists. Please sign in with your password.',
  github_auth_failed: 'GitHub authentication failed. Please try again.',
  github_no_email: 'Could not retrieve your email from GitHub. Please ensure your GitHub email is public or verified.',
  account_creation_failed: 'Could not create your account. Please try again.',
}

onMounted(() => {
  const errorCode = route.query.error as string | undefined
  if (errorCode && gitHubErrors[errorCode]) {
    error.value = gitHubErrors[errorCode]
  }
})

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
      <div class="flex items-center gap-3 my-4">
        <div class="h-px flex-1 bg-border" />
        <span class="text-xs text-muted-foreground">or</span>
        <div class="h-px flex-1 bg-border" />
      </div>
      <a
        href="/api/account/github-login"
        class="flex w-full items-center justify-center gap-2 rounded-md bg-[#24292f] px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-[#32383f]"
      >
        <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0 0 24 12c0-6.63-5.37-12-12-12z"/></svg>
        Sign in with GitHub
      </a>
    </CardContent>
  </Card>
</template>
