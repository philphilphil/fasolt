<script setup lang="ts">
import { useRoute } from 'vue-router'
import { computed } from 'vue'
import AppLayout from '@/layouts/AppLayout.vue'
import AuthLayout from '@/layouts/AuthLayout.vue'

const route = useRoute()
const isAuthPage = computed(() => route.meta.public === true)
</script>

<template>
  <component :is="isAuthPage ? AuthLayout : AppLayout">
    <RouterView />
  </component>
</template>
