<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { Button } from '@/components/ui/button'
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-background">
    <div class="text-center">
      <p class="text-6xl font-bold text-muted-foreground/30">404</p>
      <p class="mt-4 text-sm text-muted-foreground">Page not found</p>
      <RouterLink to="/dashboard" class="mt-6 inline-block">
        <Button variant="outline" size="sm">Back to dashboard</Button>
      </RouterLink>
    </div>
  </div>
</template>
