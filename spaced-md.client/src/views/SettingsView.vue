<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'

const auth = useAuthStore()

// Display name
const displayName = ref('')
const displayNameSuccess = ref(false)
const displayNameError = ref('')

// Email
const newEmail = ref('')
const emailCurrentPassword = ref('')
const emailSuccess = ref(false)
const emailError = ref('')

// Password
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
    <h1 class="text-lg font-semibold tracking-tight">Settings</h1>

    <!-- Display Name -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base">Display name</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="saveDisplayName">
          <div v-if="displayNameSuccess" class="rounded-md bg-green-500/10 px-3 py-2 text-sm text-green-600">Saved.</div>
          <div v-if="displayNameError" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ displayNameError }}</div>
          <Input v-model="displayName" placeholder="Your display name" />
          <Button type="submit" size="sm" class="self-start">Save</Button>
        </form>
      </CardContent>
    </Card>

    <!-- Email -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base">Email address</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="saveEmail">
          <div v-if="emailSuccess" class="rounded-md bg-green-500/10 px-3 py-2 text-sm text-green-600">Email updated.</div>
          <div v-if="emailError" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ emailError }}</div>
          <div class="flex flex-col gap-1.5">
            <label for="new-email" class="text-sm font-medium">New email</label>
            <Input id="new-email" v-model="newEmail" type="email" required />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="email-password" class="text-sm font-medium">Current password</label>
            <Input id="email-password" v-model="emailCurrentPassword" type="password" required autocomplete="current-password" />
          </div>
          <Button type="submit" size="sm" class="self-start">Update email</Button>
        </form>
      </CardContent>
    </Card>

    <!-- Password -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base">Change password</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="savePassword">
          <div v-if="passwordSuccess" class="rounded-md bg-green-500/10 px-3 py-2 text-sm text-green-600">Password changed.</div>
          <div v-if="passwordError" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ passwordError }}</div>
          <div class="flex flex-col gap-1.5">
            <label for="current-password" class="text-sm font-medium">Current password</label>
            <Input id="current-password" v-model="currentPassword" type="password" required autocomplete="current-password" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="new-password" class="text-sm font-medium">New password</label>
            <Input id="new-password" v-model="newPassword" type="password" required autocomplete="new-password" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="confirm-new-password" class="text-sm font-medium">Confirm new password</label>
            <Input id="confirm-new-password" v-model="confirmNewPassword" type="password" required autocomplete="new-password" />
          </div>
          <Button type="submit" size="sm" class="self-start">Change password</Button>
        </form>
      </CardContent>
    </Card>
  </div>
</template>
