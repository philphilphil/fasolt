<script setup lang="ts">
import type { Stat } from '@/types'
import StatCard from '@/components/StatCard.vue'

defineProps<{ stats: Stat[] }>()
</script>

<template>
  <div class="grid grid-cols-2 gap-2.5 sm:grid-cols-4">
    <StatCard v-for="stat in stats" :key="stat.label" :stat="stat" />
  </div>
</template>
