<script setup lang="ts">
import { ref, onMounted, defineExpose, inject } from 'vue';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import InputText from 'primevue/inputtext';
import { FilterMatchMode } from '@primevue/core/api';
import type { CardResponse } from '@/api/models';
import type { SpacedMdApiClient } from '@/api/spacedMdApiClient';
import { format } from 'date-fns'

const api = inject<SpacedMdApiClient>('api');
if (!api) throw new Error('API client not provided');

const cards = ref<CardResponse[]>([]);

async function loadData() {
  try {
    const result = await api!.cards.get();
    cards.value = result || [];
  } catch (error) {
    console.error("Failed to load cards:", error);
  }
}

function formatDate(dateString: string) {
  if (!dateString) return "-";
  return format(new Date(dateString), 'yyyy-MM-dd HH:mm')
}

defineExpose({ loadData });

onMounted(() => {
  loadData();
});

const filters = ref({
  global: { value: null, matchMode: FilterMatchMode.CONTAINS },
  name: { value: null, matchMode: FilterMatchMode.CONTAINS }
});
</script>

<template>
  <DataTable :value="cards" paginator :rows="5" :rowsPerPageOptions="[5, 10, 20, 50]" tableStyle="min-width: 50rem"
    v-model:filters="filters" filterDisplay="row"
    paginatorTemplate="FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink RowsPerPageDropdown CurrentPageReport"
    currentPageReportTemplate="Total: {totalRecords}">

    <Column field="name" header="Name" style="min-width: 10rem" sortable>
      <template #filter="{ filterModel, filterCallback }">
        <InputText v-model="filterModel.value" placeholder="Filter by name" @input="filterCallback()" />
      </template>
    </Column>
    <Column header="Uploaded at" dataType="date" style="min-width: 10rem">
      <template #body="{ data }">
        <td>{{ formatDate(data.uploadedAt) }}</td>
      </template>
    </Column>
    <Column header="Updated at" dataType="date" style="min-width: 10rem">
      <template #body="{ data }">
        <td>{{ formatDate(data.updatedAt) }}</td>
      </template>
    </Column>
    <Column field="content" header="Content preview"></Column>
  </DataTable>
</template>
