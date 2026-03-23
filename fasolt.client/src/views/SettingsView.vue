<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'

const auth = useAuthStore()

const displayName = ref('')
const displayNameSuccess = ref(false)
const displayNameError = ref('')

const newEmail = ref('')
const emailCurrentPassword = ref('')
const emailSuccess = ref(false)
const emailError = ref('')

const currentPassword = ref('')
const newPassword = ref('')
const confirmNewPassword = ref('')
const passwordSuccess = ref(false)
const passwordError = ref('')

onMounted(() => {
  displayName.value = auth.user?.displayName || ''
  newEmail.value = auth.user?.email || ''
})

async function saveDisplayName() {
  displayNameSuccess.value = false
  displayNameError.value = ''
  try {
    await auth.updateProfile(displayName.value || null)
    displayNameSuccess.value = true
  } catch (e) {
    displayNameError.value = 'Failed to update display name.'
  }
}

async function saveEmail() {
  emailSuccess.value = false
  emailError.value = ''
  try {
    await auth.changeEmail(newEmail.value, emailCurrentPassword.value)
    emailCurrentPassword.value = ''
    emailSuccess.value = true
  } catch (e) {
    if (isApiError(e) && e.errors) {
      emailError.value = Object.values(e.errors).flat().join(' ')
    } else {
      emailError.value = 'Failed to update email.'
    }
  }
}

async function savePassword() {
  passwordSuccess.value = false
  passwordError.value = ''
  if (newPassword.value !== confirmNewPassword.value) {
    passwordError.value = 'Passwords do not match.'
    return
  }
  try {
    await auth.changePassword(currentPassword.value, newPassword.value)
    currentPassword.value = ''
    newPassword.value = ''
    confirmNewPassword.value = ''
    passwordSuccess.value = true
  } catch (e) {
    if (isApiError(e) && e.errors) {
      passwordError.value = Object.values(e.errors).flat().join(' ')
    } else {
      passwordError.value = 'Failed to update password.'
    }
  }
}
</script>

<template>
  <div class="flex flex-col gap-6">
    <h1 class="text-xl font-bold tracking-tight">Settings</h1>

    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">Display name</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="saveDisplayName">
          <div v-if="displayNameSuccess" class="rounded border border-success/20 bg-success/10 px-3 py-2 text-xs text-success">Saved.</div>
          <div v-if="displayNameError" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ displayNameError }}</div>
          <Input v-model="displayName" placeholder="Your display name" />
          <Button type="submit" size="sm" class="self-start text-xs">Save</Button>
        </form>
      </CardContent>
    </Card>

    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">Email address</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="saveEmail">
          <div v-if="emailSuccess" class="rounded border border-success/20 bg-success/10 px-3 py-2 text-xs text-success">Email updated.</div>
          <div v-if="emailError" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ emailError }}</div>
          <div class="flex flex-col gap-1.5">
            <label for="new-email" class="text-xs font-medium">New email</label>
            <Input id="new-email" v-model="newEmail" type="email" required />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="email-password" class="text-xs font-medium">Current password</label>
            <Input id="email-password" v-model="emailCurrentPassword" type="password" required autocomplete="off" />
          </div>
          <Button type="submit" size="sm" class="self-start text-xs">Update email</Button>
        </form>
      </CardContent>
    </Card>

    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">Change password</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="savePassword">
          <div v-if="passwordSuccess" class="rounded border border-success/20 bg-success/10 px-3 py-2 text-xs text-success">Password changed.</div>
          <div v-if="passwordError" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ passwordError }}</div>
          <div class="flex flex-col gap-1.5">
            <label for="current-password" class="text-xs font-medium">Current password</label>
            <Input id="current-password" v-model="currentPassword" type="password" required autocomplete="current-password" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="new-password" class="text-xs font-medium">New password</label>
            <Input id="new-password" v-model="newPassword" type="password" required autocomplete="new-password" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="confirm-new-password" class="text-xs font-medium">Confirm new password</label>
            <Input id="confirm-new-password" v-model="confirmNewPassword" type="password" required autocomplete="new-password" />
          </div>
          <Button type="submit" size="sm" class="self-start text-xs">Change password</Button>
        </form>
      </CardContent>
    </Card>
  </div>
</template>
