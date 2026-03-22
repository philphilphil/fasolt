<script setup lang="ts">
import type { Stat } from '@/types'
import { Card, CardContent } from '@/components/ui/card'

defineProps<{ stat: Stat }>()
</script>

<template>
  <Card class="border-border" :aria-label="`${stat.value} ${stat.label}`">
    <CardContent class="p-3">
      <div class="font-mono text-[22px] font-bold tracking-tight text-foreground">
        {{ stat.value }}
      </div>
      <div class="mt-0.5 text-xs uppercase tracking-widest text-muted-foreground">
        {{ stat.label }}
      </div>
      <div v-if="stat.delta" class="mt-1 text-xs text-success">
        {{ stat.delta }}
      </div>
    </CardContent>
  </Card>
</template>
