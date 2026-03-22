<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { Button } from '@/components/ui/button'
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-background">
    <div class="text-center">
      <p class="text-5xl font-bold text-accent/20">404</p>
      <p class="mt-3 text-xs text-muted-foreground">Page not found</p>
      <RouterLink to="/dashboard" class="mt-6 inline-block">
        <Button variant="outline" size="sm" class="text-xs">Back to dashboard</Button>
      </RouterLink>
    </div>
  </div>
</template>
