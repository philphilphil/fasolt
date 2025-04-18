<script setup lang="ts">
import { ref } from 'vue';
import Card from 'primevue/card';
import Divider from 'primevue/divider';
import Panel from 'primevue/panel';

interface CardItem {
  id: number;
  title: string;
  content: string;
}

const infoText = `Study your markdown files with our spaced repetition system.
Keep track of key points, revise regularly, and boost your retention.
Each cue card below represents a question or concept extracted from your markdown notes.`;

const cards = ref<CardItem[]>([
  { id: 1, title: 'Cue Card 1', content: 'This is the first cue card.' },
  { id: 2, title: 'Cue Card 2', content: 'This is the second cue card.' },
  { id: 3, title: 'Cue Card 3', content: 'This is the third cue card.' }
]);
</script>

<template>
  <div class="study-container">
    <!-- Header Panel with info -->
    <Panel header="Study Your Markdown Files" class="mb-4">
      <p>{{ infoText }}</p>
    </Panel>

    <Divider align="center">Cue Cards</Divider>

    <!-- Display cue cards -->
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
  </div>
</template>

<style scoped>
.study-container {
  padding: 1rem;
}

.card-container {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  padding: 1rem 0;
}

.cue-card {
  flex: 1 1 calc(33.333% - 1rem);
  box-sizing: border-box;
  min-width: 250px;
}
</style>
