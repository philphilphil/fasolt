<script setup lang="ts">
import { useRoute } from 'vue-router'
import { computed } from 'vue'
import AppLayout from '@/layouts/AppLayout.vue'

const route = useRoute()
const isLanding = computed(() => route.meta.landing === true)
</script>

<template>
  <template v-if="isLanding">
    <RouterView />
  </template>
  <AppLayout v-else>
    <RouterView />
  </AppLayout>
</template>
