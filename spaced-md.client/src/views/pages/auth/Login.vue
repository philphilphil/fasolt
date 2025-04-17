<script setup lang="ts">
import { ref, inject } from 'vue';
import { useRouter } from 'vue-router';
import type { SpacedMdApiClient } from '@/api/spacedMdApiClient';
import type { LoginRequestBuilderPostQueryParameters } from '@/api/login';

import InputText from 'primevue/inputtext';
import Password from 'primevue/password';
import Button from 'primevue/button';

const api = inject<SpacedMdApiClient>('api');
if (!api) throw new Error('API client not provided');

const router = useRouter();

const email = ref('');
const password = ref('');
const errorMessage = ref('');

const handleLogin = async () => {
  try {
    const loginQueryParameters: LoginRequestBuilderPostQueryParameters = {
      useCookies: true,
      useSessionCookies: false,
    };

    await api.login.post(
      {
        email: email.value,
        password: password.value,
      },
      { queryParameters: loginQueryParameters }
    );
    router.push('/');
  } catch (error) {
    errorMessage.value = "Error during login." + error;
  }
};
</script>

<template>
  <div class="min-h-screen flex flex-col justify-center items-center bg-gray-100 p-4">
    <div class="bg-white shadow-md rounded-lg p-8 w-full max-w-md">
      <div class="flex justify-center mb-6">
        <img src="../../../assets/images/logo_full.png" alt="Logo" class="h-50" />
      </div>
      <h2 class="text-2xl font-bold text-center mb-4">Login</h2>
      <div class="mb-4">
        <label class="block font-bold text-gray-700 mb-2">Email</label>
        <InputText v-model="email" type="text" placeholder="Enter your email" class="w-full" />
      </div>
      <div class="mb-4">
        <label class="block font-bold text-gray-700 mb-2">Password</label>
        <Password v-model="password" placeholder="Enter your password" toggleMask class="w-full" :feedback="false"
          :style="{ width: '100%' }" :inputStyle="{ width: '100%' }" />
      </div>
      <Button label="Sign In" class="w-full" @click="handleLogin" />
      <div v-if="errorMessage" class="mt-4 text-red-500 text-sm text-center">
        {{ errorMessage }}
      </div>
    </div>
  </div>
</template>


<style scoped>
/* Add any additional custom styling here */
</style>
