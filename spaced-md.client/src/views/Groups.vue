<script setup lang="ts">
import { ref } from 'vue';
import Card from 'primevue/card';
import Button from 'primevue/button';
import Dialog from 'primevue/dialog';

interface CardItem {
  id: number;
  title: string;
  content: string;
}

interface CardGroup {
  id: number;
  name: string;
  cards: CardItem[];
}

const cardGroups = ref<CardGroup[]>([
  {
    id: 1,
    name: 'Group A',
    cards: [
      { id: 1, title: 'Card 1', content: 'Content for card 1' },
      { id: 2, title: 'Card 2', content: 'Content for card 2' }
    ]
  },
  {
    id: 2,
    name: 'Group B',
    cards: [
      { id: 3, title: 'Card 3', content: 'Content for card 3' },
      { id: 4, title: 'Card 4', content: 'Content for card 4' }
    ]
  }
]);

const studyDialog = ref(false);
const currentStudyGroup = ref<CardGroup | null>(null);

function studyGroup(group: CardGroup) {
  currentStudyGroup.value = group;
  studyDialog.value = true;
}
</script>



<template>
  <div class="groups">
    <h1>Study Groups</h1>
    <div v-for="group in cardGroups" :key="group.id" class="group-card">
      <Card>
        <template #title>
          <h2>{{ group.name }}</h2>
        </template>
        <template #content>
          <ul>
            <li v-for="card in group.cards" :key="card.id">
              {{ card.title }} – {{ card.content }}
            </li>
          </ul>
          <Button label="Study Group" icon="pi pi-book" @click="studyGroup(group)" />
        </template>
      </Card>
    </div>

    <Dialog v-model:visible="studyDialog" header="Study Session" modal>
      <div v-if="currentStudyGroup">
        <h3>{{ currentStudyGroup.name }}</h3>
        <div v-for="card in currentStudyGroup.cards" :key="card.id" class="study-card">
          <Card>
            <template #title>
              <h4>{{ card.title }}</h4>
            </template>
            <template #content>
              <p>{{ card.content }}</p>
            </template>
          </Card>
        </div>
      </div>
      <template #footer>
        <Button label="Close" icon="pi pi-times" @click="studyDialog = false" />
      </template>
    </Dialog>
  </div>
</template>


<style scoped>
.groups {
  padding: 2rem;
}

.group-card {
  margin-bottom: 1rem;
}

.study-card {
  margin-bottom: 1rem;
}
</style>
