<script setup lang="ts">
import { RouterLink } from 'vue-router'
import FasoltWordmark from '@/components/FasoltWordmark.vue'
import ThemeToggle from '@/components/ThemeToggle.vue'
</script>

<template>
  <nav class="public-nav sticky top-0 z-50 border-b border-rule-1 bg-paper-0">
    <div class="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
      <RouterLink to="/" class="flex items-center">
        <FasoltWordmark :size="32" />
      </RouterLink>
      <div class="flex items-center gap-2">
        <RouterLink to="/library" class="fa-btn fa-btn-ghost">Library</RouterLink>
        <ThemeToggle />
        <a href="/login" class="fa-btn fa-btn-ghost">Log in</a>
        <a href="/register" class="fa-btn fa-btn-primary">Sign up</a>
      </div>
    </div>
  </nav>
</template>
