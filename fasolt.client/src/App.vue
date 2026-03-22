<script setup lang="ts">
import { useRoute } from 'vue-router'
import { computed } from 'vue'
import AppLayout from '@/layouts/AppLayout.vue'
import AuthLayout from '@/layouts/AuthLayout.vue'

const route = useRoute()
const isLanding = computed(() => route.meta.landing === true)
const isAuthPage = computed(() => route.meta.public === true && !isLanding.value)
</script>

<template>
  <template v-if="isLanding">
    <RouterView />
  </template>
  <component v-else :is="isAuthPage ? AuthLayout : AppLayout">
    <RouterView />
  </component>
</template>
