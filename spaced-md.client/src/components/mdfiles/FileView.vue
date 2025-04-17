<script setup lang="ts">
import type { MdFileResponse } from '@/api/models';
import type { SpacedMdApiClient } from '@/api/spacedMdApiClient';
import { ref, onMounted } from 'vue'
import { inject } from 'vue';

const api = inject<SpacedMdApiClient>('api');
if (!api) throw new Error('API client not provided');

var mdFIles = ref<MdFileResponse[]>([]);

onMounted(async () => {
    try {
        const result = await api.mdfile.get();
        mdFIles.value = result || [];
    } catch (error) {
        console.error("Failed to fetch weather forecast:", error);
    }
});
</script>

<template>
<table border="1" class="table-auto w-full text-left">
  <thead>
    <tr>
      <th class="px-4 py-2">Id</th>
      <th class="px-4 py-2">Name</th>
      <th class="px-4 py-2">Content</th>
    </tr>
  </thead>
  <tbody>
    <tr v-for="item in mdFIles" :key="item.id">
      <td class="border px-4 py-2">{{ item.id }}</td>
      <td class="border px-4 py-2">{{ item.fileName }}</td>
      <td class="border px-4 py-2">{{ item.content.length > 100 ? item.content.slice(0, 100) + '...' : item.content }}</td>
    </tr>
  </tbody>
</table>
</template>

<style scoped>
a {
  color: #42b983;
}

label {
  margin: 0 0.5em;
  font-weight: bold;
}

code {
  background-color: #eee;
  padding: 2px 4px;
  border-radius: 4px;
  color: #304455;
}
</style>
