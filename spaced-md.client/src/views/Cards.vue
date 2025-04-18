<script setup lang="ts">
import { ref } from 'vue';
import Card from 'primevue/card';

interface CardItem {
  id: number;
  title: string;
  content: string;
}

const cards = ref<CardItem[]>([
  { id: 1, title: 'Card 1', content: 'This is the first cue card.' },
  { id: 2, title: 'Card 2', content: 'This is the second cue card.' },
  { id: 3, title: 'Card 3', content: 'This is the third cue card.' }
]);
</script>


<template>
  <div class="card-container">
    <Card
      v-for="card in cards"
      :key="card.id"
      :title="card.title"
      class="cue-card"
    >
      <template #content>
        <p>{{ card.content }}</p>
      </template>
    </Card>
  </div>
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
