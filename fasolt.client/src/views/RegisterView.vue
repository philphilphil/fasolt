<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'
import { usePasswordRules } from '@/composables/usePasswordRules'

const router = useRouter()
const auth = useAuthStore()

const email = ref('')
const password = ref('')
const confirmPassword = ref('')
const errors = ref<string[]>([])
const loading = ref(false)

const { rules: passwordRules, allValid: passwordValid } = usePasswordRules(password)

const passwordsMatch = computed(() => password.value === confirmPassword.value)

const canSubmit = computed(
  () =>
    email.value &&
    passwordValid.value &&
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
      <div v-if="errors.length" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive mb-4">
        <p v-for="err in errors" :key="err">{{ err }}</p>
      </div>
      <a
        href="/api/account/github-login"
        class="flex w-full items-center justify-center gap-2 rounded-md bg-[#24292f] px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-[#32383f]"
      >
        <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0 0 24 12c0-6.63-5.37-12-12-12z"/></svg>
        Sign up with GitHub
      </a>
      <div class="flex items-center gap-3 my-4">
        <div class="h-px flex-1 bg-border" />
        <span class="text-xs text-muted-foreground">or</span>
        <div class="h-px flex-1 bg-border" />
      </div>
      <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
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
