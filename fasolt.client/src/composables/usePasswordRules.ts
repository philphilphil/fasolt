import { computed, type Ref } from 'vue'

export function usePasswordRules(password: Ref<string>) {
  const rules = computed(() => [
    { label: 'At least 8 characters', valid: password.value.length >= 8 },
    { label: 'Uppercase letter', valid: /[A-Z]/.test(password.value) },
    { label: 'Lowercase letter', valid: /[a-z]/.test(password.value) },
    { label: 'Number', valid: /\d/.test(password.value) },
  ])

  const allValid = computed(() => rules.value.every(r => r.valid))

  return { rules, allValid }
}
