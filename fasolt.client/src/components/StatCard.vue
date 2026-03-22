<script setup lang="ts">
import type { Stat } from '@/types'
import { Card, CardContent } from '@/components/ui/card'

defineProps<{ stat: Stat }>()
</script>

<template>
  <Card class="border-border/60 hover:border-accent/30 transition-colors" :aria-label="`${stat.value} ${stat.label}`">
    <CardContent class="p-4">
      <div class="text-[22px] font-bold tracking-tight text-foreground">
        {{ stat.value }}
      </div>
      <div class="mt-1 text-[10px] uppercase tracking-[0.15em] text-muted-foreground">
        {{ stat.label }}
      </div>
      <div v-if="stat.delta" class="mt-1.5 text-xs text-success">
        {{ stat.delta }}
      </div>
    </CardContent>
  </Card>
</template>
