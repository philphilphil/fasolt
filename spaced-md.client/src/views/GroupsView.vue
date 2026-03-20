<script setup lang="ts">
import { ref } from 'vue'
import { useGroupsStore } from '@/stores/groups'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,
} from '@/components/ui/dialog'

const groups = useGroupsStore()
const newGroupName = ref('')
const dialogOpen = ref(false)

function createGroup() {
  if (newGroupName.value.trim()) {
    groups.addGroup(newGroupName.value.trim())
    newGroupName.value = ''
    dialogOpen.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between">
      <h1 class="text-lg font-semibold tracking-tight">Groups</h1>
      <Dialog v-model:open="dialogOpen">
        <DialogTrigger as-child>
          <Button size="sm" class="text-xs">New group</Button>
        </DialogTrigger>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create group</DialogTitle>
          </DialogHeader>
          <Input v-model="newGroupName" placeholder="Group name" @keydown.enter="createGroup" />
          <DialogFooter>
            <Button @click="createGroup">Create</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>

    <div class="grid gap-2.5 sm:grid-cols-2">
      <Card v-for="group in groups.groups" :key="group.id" class="cursor-pointer border-border">
        <CardContent class="flex items-center justify-between p-4">
          <div>
            <div class="text-sm font-medium text-foreground">{{ group.name }}</div>
            <div class="mt-0.5 text-[11px] text-muted-foreground">
              {{ group.cardCount }} cards
            </div>
          </div>
          <div class="flex items-center gap-3">
            <span v-if="group.dueCount > 0" class="font-mono text-xs text-warning">
              {{ group.dueCount }} due
            </span>
            <Button
              variant="ghost"
              size="sm"
              class="h-6 text-[10px] text-destructive hover:text-destructive"
              @click.stop="groups.deleteGroup(group.id)"
            >
              Delete
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>

    <div v-if="groups.groups.length === 0" class="py-12 text-center text-sm text-muted-foreground">
      No groups yet. Create one to organize your cards.
    </div>
  </div>
</template>
