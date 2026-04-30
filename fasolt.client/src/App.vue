<script setup lang="ts">
import { useRoute } from 'vue-router'
import { computed } from 'vue'
import AppLayout from '@/layouts/AppLayout.vue'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const auth = useAuthStore()
const isLanding = computed(
  () => route.meta.landing === true || (route.meta.public === true && !auth.isAuthenticated),
)
</script>

<template>
  <template v-if="isLanding">
    <RouterView />
  </template>
  <AppLayout v-else>
    <RouterView />
  </AppLayout>
</template>
