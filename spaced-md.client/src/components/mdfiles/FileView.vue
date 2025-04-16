<script setup lang="ts">
import { Client, WeatherForecast } from '@/api/apiClient';
import type { AxiosInstance } from 'axios';
import { ref, onMounted } from 'vue'
import { inject } from 'vue';


const api = inject<Client>('api')
if (!api) throw new Error('API client not provided')

const forecast = ref<WeatherForecast[]>([])

onMounted(async () => {
    try {
        const result = await api.getWeatherforecast();
        forecast.value = result;
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
