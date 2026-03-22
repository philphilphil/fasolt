<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'

const route = useRoute()
const auth = useAuthStore()

const emailParam = (route.query.email as string) || ''
const tokenParam = (route.query.token as string) || ''

const password = ref('')
const confirmPassword = ref('')
const error = ref('')
const success = ref(false)
const loading = ref(false)

const passwordRules = computed(() => [
  { label: 'At least 8 characters', valid: password.value.length >= 8 },
  { label: 'Uppercase letter', valid: /[A-Z]/.test(password.value) },
  { label: 'Lowercase letter', valid: /[a-z]/.test(password.value) },
  { label: 'Number', valid: /\d/.test(password.value) },
])

const passwordsMatch = computed(() => password.value === confirmPassword.value)

const canSubmit = computed(
  () => passwordRules.value.every((r) => r.valid) && passwordsMatch.value && !loading.value,
)

async function handleSubmit() {
  error.value = ''
  loading.value = true
  try {
    await auth.resetPassword(emailParam, tokenParam, password.value)
    success.value = true
  } catch (e) {
    if (isApiError(e) && e.errors) {
      error.value = Object.values(e.errors).flat().join(' ')
    } else {
      error.value = 'Something went wrong. Please try again.'
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <Card>
    <CardHeader>
      <CardTitle class="text-center text-lg">Set new password</CardTitle>
    </CardHeader>
    <CardContent>
      <template v-if="success">
        <p class="text-center text-sm text-muted-foreground">
          Your password has been reset.
        </p>
        <div class="mt-4 text-center">
          <RouterLink to="/login" class="text-sm text-accent hover:underline">Log in</RouterLink>
        </div>
      </template>
      <template v-else-if="!emailParam || !tokenParam">
        <p class="text-center text-sm text-destructive">Invalid or missing reset link.</p>
        <div class="mt-4 text-center">
          <RouterLink to="/forgot-password" class="text-sm text-accent hover:underline">Request a new link</RouterLink>
        </div>
      </template>
      <form v-else class="flex flex-col gap-4" @submit.prevent="handleSubmit">
        <div v-if="error" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {{ error }}
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="password" class="text-sm font-medium">New password</label>
          <Input id="password" v-model="password" type="password" required autocomplete="new-password" />
          <ul v-if="password" class="mt-1 space-y-0.5 text-xs">
            <li v-for="rule in passwordRules" :key="rule.label" :class="rule.valid ? 'text-green-600' : 'text-muted-foreground'">
              {{ rule.valid ? '\u2713' : '\u25CB' }} {{ rule.label }}
            </li>
          </ul>
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="confirm-password" class="text-sm font-medium">Confirm new password</label>
          <Input id="confirm-password" v-model="confirmPassword" type="password" required autocomplete="new-password" />
          <p v-if="confirmPassword && !passwordsMatch" class="text-xs text-destructive">Passwords do not match.</p>
        </div>
        <Button type="submit" class="w-full" :disabled="!canSubmit">
          {{ loading ? 'Resetting\u2026' : 'Reset password' }}
        </Button>
      </form>
    </CardContent>
  </Card>
</template>
