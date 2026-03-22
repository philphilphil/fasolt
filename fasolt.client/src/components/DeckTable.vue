<script setup lang="ts">
import type { Deck } from '@/types'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'

defineProps<{ decks: Deck[] }>()
defineEmits<{ 'select-deck': [deck: Deck] }>()
</script>

<template>
  <Table>
    <TableHeader>
      <TableRow class="text-[10px] uppercase tracking-wider text-muted-foreground hover:bg-transparent">
        <TableHead class="h-8">Deck</TableHead>
        <TableHead class="h-8">Due</TableHead>
        <TableHead class="h-8">Cards</TableHead>
        <TableHead class="h-8 hidden sm:table-cell">Description</TableHead>
      </TableRow>
    </TableHeader>
    <TableBody>
      <TableRow
        v-for="deck in decks"
        :key="deck.id"
        class="cursor-pointer text-xs"
        @click="$emit('select-deck', deck)"
      >
        <TableCell class="font-medium text-foreground">{{ deck.name }}</TableCell>
        <TableCell class="font-mono text-warning">{{ deck.dueCount }}</TableCell>
        <TableCell class="font-mono text-muted-foreground">{{ deck.cardCount }}</TableCell>
        <TableCell class="hidden text-muted-foreground sm:table-cell">{{ deck.description || '—' }}</TableCell>
      </TableRow>
    </TableBody>
  </Table>
</template>
