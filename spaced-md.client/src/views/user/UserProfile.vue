<script setup lang="ts">
import { inject, reactive } from 'vue';
import InputText from 'primevue/inputtext';
import Password from 'primevue/password';
import Button from 'primevue/button';
import type { SpacedMdApiClient } from '@/api/spacedMdApiClient';
import { useToastService } from '@/service/toastService';
import { useToast } from 'primevue';

const api = inject<SpacedMdApiClient>('api');
if (!api) throw new Error('API client not provided');

const toast = useToast();
const { tempSuccessToast, failToast } = useToastService(toast);

const emailForm = reactive({
  currentPassword: '',
  newEmail: ''
});

const passwordForm = reactive({
  currentPassword: '',
  newPassword: '',
  confirmPassword: ''
});

function changeEmail(): void {

  api?.user.info.post({
    oldPassword: emailForm.currentPassword,
    newEmail: emailForm.newEmail
  }).then(() => {
    tempSuccessToast('Email updated successfully.');
  }).catch((error) => {
    if (error.additionalData) {
      failToast(error.additionalData.detail);
    } else {
      failToast('An error occurred while updating the email.');
    }
  });

  emailForm.currentPassword = '';
  emailForm.newEmail = '';
}

function changePassword(): void {
  const { currentPassword, newPassword, confirmPassword } = passwordForm;
  if (newPassword !== confirmPassword) {
    failToast('New passwords do not match.');
    return;
  }

  api?.user.info.post({
    oldPassword: currentPassword,
    newPassword: newPassword
  }).then(() => {
    tempSuccessToast('Password updated successfully.');
  }).catch((error) => {
    if (error.additionalData) {
      failToast(error.additionalData.detail);
    } else {
      failToast('An error occurred while updating the email.');
    }
  });

  passwordForm.currentPassword = '';
  passwordForm.newPassword = '';
  passwordForm.confirmPassword = '';
}
</script>

<template>
  <div class="user-profile">
    <h1>User Profile</h1>

    <section class="change-email">
      <h2>Change Email</h2>
      <form @submit.prevent="changeEmail">
        <div class="form-group">
          <label for="currentPasswordEmail">Current Password:</label>
          <Password id="currentPasswordEmail" v-model="emailForm.currentPassword" toggleMask required :feedback="false"
            :style="{ width: '100%' }" :inputStyle="{ width: '100%' }" />
        </div>
        <div class="form-group">
          <label for="newEmail">New Email:</label>
          <InputText id="newEmail" type="email" v-model="emailForm.newEmail" required />
        </div>
        <Button label="Update Email" type="submit" />
      </form>
    </section>

    <section class="change-password">
      <h2>Change Password</h2>
      <form @submit.prevent="changePassword">
        <div class="form-group">
          <label for="currentPassword">Current Password:</label>
          <Password id="currentPassword" v-model="passwordForm.currentPassword" toggleMask required :feedback="false"
            :style="{ width: '100%' }" :inputStyle="{ width: '100%' }" />
        </div>
        <div class="form-group">
          <label for="newPassword">New Password:</label>
          <Password id="newPassword" v-model="passwordForm.newPassword" toggleMask required :feedback="false"
            :style="{ width: '100%' }" :inputStyle="{ width: '100%' }" />
        </div>
        <div class="form-group">
          <label for="confirmPassword">Confirm New Password:</label>
          <Password id="confirmPassword" v-model="passwordForm.confirmPassword" toggleMask required :feedback="false"
            :style="{ width: '100%' }" :inputStyle="{ width: '100%' }" />
        </div>
        <Button label="Update Password" type="submit" />
      </form>
    </section>
  </div>
</template>

<style scoped>
.user-profile {
  max-width: 600px;
  margin: 0 auto;
  padding: 1rem;
}

.user-profile h1,
.user-profile h2 {
  text-align: center;
}

form {
  margin-bottom: 2rem;
  border: 1px solid #ccc;
  padding: 1rem;
  border-radius: 4px;
}

.form-group {
  margin-bottom: 1rem;
}

label {
  display: block;
  margin-bottom: 0.3rem;
}

input {
  width: 100%;
  padding: 0.5rem;
  box-sizing: border-box;
}

button {
  display: block;
  margin: 0 auto;
  padding: 0.5rem 1rem;
  cursor: pointer;
}
</style>
