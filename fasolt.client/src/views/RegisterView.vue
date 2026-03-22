<script setup lang="ts">
import { ref, computed } from 'vue'
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
const confirmPassword = ref('')
const errors = ref<string[]>([])
const loading = ref(false)

const passwordRules = computed(() => [
  { label: 'At least 8 characters', valid: password.value.length >= 8 },
  { label: 'Uppercase letter', valid: /[A-Z]/.test(password.value) },
  { label: 'Lowercase letter', valid: /[a-z]/.test(password.value) },
  { label: 'Number', valid: /\d/.test(password.value) },
])

const passwordsMatch = computed(() => password.value === confirmPassword.value)

const canSubmit = computed(
  () =>
    email.value &&
    passwordRules.value.every((r) => r.valid) &&
    passwordsMatch.value &&
    !loading.value,
)

async function handleSubmit() {
  errors.value = []
  loading.value = true
  try {
    await auth.register(email.value, password.value)
    router.push('/')
  } catch (e) {
    if (isApiError(e) && e.errors) {
      errors.value = Object.values(e.errors).flat()
    } else {
      errors.value = ['Something went wrong. Please try again.']
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <Card class="border-border/60">
    <CardHeader>
      <CardTitle class="text-center text-base">Create account</CardTitle>
    </CardHeader>
    <CardContent>
      <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
        <div v-if="errors.length" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">
          <p v-for="err in errors" :key="err">{{ err }}</p>
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="email" class="text-xs font-medium">Email</label>
          <Input id="email" v-model="email" type="email" required autocomplete="email" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="password" class="text-xs font-medium">Password</label>
          <Input id="password" v-model="password" type="password" required autocomplete="new-password" />
          <ul v-if="password" class="mt-1 space-y-0.5 text-[11px]">
            <li v-for="rule in passwordRules" :key="rule.label" :class="rule.valid ? 'text-success' : 'text-muted-foreground'">
              {{ rule.valid ? '\u2713' : '\u25CB' }} {{ rule.label }}
            </li>
          </ul>
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="confirm-password" class="text-xs font-medium">Confirm password</label>
          <Input id="confirm-password" v-model="confirmPassword" type="password" required autocomplete="new-password" />
          <p v-if="confirmPassword && !passwordsMatch" class="text-[11px] text-destructive">Passwords do not match.</p>
        </div>
        <Button type="submit" class="w-full" :disabled="!canSubmit">
          {{ loading ? 'Creating account\u2026' : 'Create account' }}
        </Button>
        <p class="text-center text-xs">
          Already have an account? <RouterLink to="/login" class="text-accent hover:underline">Log in</RouterLink>
        </p>
      </form>
    </CardContent>
  </Card>
</template>
