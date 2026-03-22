<script setup lang="ts">
import type { ReviewRating } from '@/types'
import { Button } from '@/components/ui/button'

defineEmits<{ rate: [rating: ReviewRating] }>()

const ratings: { key: string; label: string; rating: ReviewRating; color: string }[] = [
  { key: '1', label: 'Again', rating: 'again', color: 'border-destructive/50 text-destructive hover:bg-destructive/10' },
  { key: '2', label: 'Hard', rating: 'hard', color: 'border-warning/50 text-warning hover:bg-warning/10' },
  { key: '3', label: 'Good', rating: 'good', color: 'border-success/50 text-success hover:bg-success/10' },
  { key: '4', label: 'Easy', rating: 'easy', color: 'border-accent/50 text-accent hover:bg-accent/10' },
]
</script>

<template>
  <div class="flex justify-center gap-2">
    <Button
      v-for="r in ratings"
      :key="r.rating"
      variant="outline"
      class="text-xs py-3"
      :class="r.color"
      @click="$emit('rate', r.rating)"
    >
      <span class="hidden text-muted-foreground sm:inline mr-1 text-[10px]">{{ r.key }}</span>
      {{ r.label }}
    </Button>
  </div>
</template>
