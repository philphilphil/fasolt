<script setup lang="ts">
import { ref } from 'vue';
import Card from 'primevue/card';
import CardDialog from '@/components/cards/AddCardDialog.vue';
import CardsDataTableComponent from '@/components/cards/CardsDataTableComponent.vue';

const dialogVisible = ref(false);
const cardToEdit = ref<string | null>("");

function openAddDialog() {
  cardToEdit.value = null;
  dialogVisible.value = true;
}

function openEditDialog(id: string) {
  cardToEdit.value = id;
  dialogVisible.value = true;
}

function refreshCards() {
  // Trigger a reload in CardsDataTableComponent
}
</script>

<template>
  <Button label="Add Card" icon="pi pi-plus" @click="openAddDialog" class="mb-5"/>
  <CardsDataTableComponent @editCard="openEditDialog" />
  <CardDialog v-model:visible="dialogVisible" :cardId="cardToEdit!" @refreshCards="refreshCards" />
  Todo:<br />
  - Add automatic generation of cards from a file (a card for each heading)
</template>

<style scoped>
.card-container {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  padding: 1rem;
}

.cue-card {
  flex: 1 1 calc(33.333% - 1rem);
  box-sizing: border-box;
  min-width: 250px;
}
</style>
