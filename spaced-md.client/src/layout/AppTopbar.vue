<script setup>
import { inject, onMounted } from 'vue';
import { useLayout } from '@/layout/composables/layout';
import { useAuthStore } from '@/stores/authStore'
import { useAuthService } from '@/service/authService';

const api = inject('api');
if (!api) throw new Error('API client not provided');

const authStore = useAuthStore()
const { toggleDarkMode, isDarkTheme } = useLayout();
const { logout, getUserInfo } = useAuthService(api);

onMounted(async () => {
  getUserInfo();
});

</script>

<template>
  <div class="layout-topbar">
    <div class="layout-topbar-logo-container">
      <button class="layout-menu-button layout-topbar-action" @click="toggleMenu">
        <i class="pi pi-bars"></i>
      </button>
      <router-link to="/" class="layout-topbar-logo">
        <img src="../assets/images/logo.png" alt="Logo" width="40px" />
        <span>spaced md</span>
      </router-link>
    </div>

    <div class="layout-topbar-actions">
      <div class="layout-config-menu">
        <button type="button" class="layout-topbar-action" @click="toggleDarkMode">
          <i :class="['pi', { 'pi-moon': isDarkTheme, 'pi-sun': !isDarkTheme }]"></i>
        </button>
      </div>

      <button class="layout-topbar-menu-button layout-topbar-action"
        v-styleclass="{ selector: '@next', enterFromClass: 'hidden', enterActiveClass: 'animate-scalein', leaveToClass: 'hidden', leaveActiveClass: 'animate-fadeout', hideOnOutsideClick: true }">
        <i class="pi pi-ellipsis-v"></i>
      </button>

      <div class="layout-topbar-menu hidden lg:block">
        <div class="layout-topbar-menu-content">

          <button type="button" class="layout-topbar-action">
            <i class="pi pi-user"></i>
            <span>Profile</span>
          </button>
          <span v-if="authStore.isAuthenticated">
            <router-link to="/profile">
              {{ authStore.user?.email }}
            </router-link>
            <button type="button" class="layout-topbar-action" @click="logout">
              <i class="pi pi-sign-out"></i>
            </button>
          </span>
          <span v-else>
            <router-link to="/auth/login">
              Not logged in
            </router-link>
          </span>
        </div>
      </div>
    </div>
  </div>
</template>
