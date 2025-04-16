<script setup lang="ts">
import type { WeatherForecast } from '@/api/models';
import type { SpacedMdApiClient } from '@/api/spacedMdApiClient';
import { ref, onMounted } from 'vue'
import { inject } from 'vue';

const api = inject<SpacedMdApiClient>('api');
if (!api) throw new Error('API client not provided');

var forecast = ref<WeatherForecast[]>([]);

onMounted(async () => {
    try {
        const result = await api.weatherforecast.get();
        forecast.value = result || [];
    } catch (error) {
        console.error("Failed to fetch weather forecast:", error);
    }
});
</script>

<template>
  asdaa
<table>
  <thead>
    <tr>
      <th>Date</th>
      <th>Temperature (C)</th>
      <th>Summary</th>
    </tr>
  </thead>
  <tbody>
    <tr v-for="item in forecast">
      <td>{{ item.date }}</td>
      <td>{{ item.temperatureC }}</td>
      <td>{{ item.summary }}</td>
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
