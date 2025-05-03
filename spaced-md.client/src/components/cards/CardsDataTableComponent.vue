<script setup lang="ts">
import { ref, onMounted, defineExpose, inject, defineEmits } from 'vue';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import InputText from 'primevue/inputtext';
import { FilterMatchMode } from '@primevue/core/api';
import type { CardResponse } from '@/api/models';
import type { SpacedMdApiClient } from '@/api/spacedMdApiClient';
import { format } from 'date-fns'
import { useConfirm } from "primevue/useconfirm";

const confirm = useConfirm();
const emit = defineEmits(['editCard']);

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

function deleteCard(id: string) {
  confirm.require({
    message: 'Are you sure you want to delete this item?',
    header: 'Delete Confirmation',
    icon: 'pi pi-exclamation-triangle',
    accept: () => {
      api!.cards.byId(id).delete().then(() => {
        loadData();
      }).catch((error) => {
        console.error("Failed to delete card:", error);
      });
    },
    reject: () => {
    }
  });
}

function editCard(id: string) {
  emit('editCard', id);
}

const filters = ref({
  global: { value: null, matchMode: FilterMatchMode.CONTAINS },
  title: { value: null, matchMode: FilterMatchMode.CONTAINS }
});
</script>

<template>
  <DataTable :value="cards" paginator :rows="5" :rowsPerPageOptions="[5, 10, 20, 50]" tableStyle="min-width: 50rem"
    v-model:filters="filters" filterDisplay="row"
    paginatorTemplate="FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink RowsPerPageDropdown CurrentPageReport"
    currentPageReportTemplate="Total: {totalRecords}">
    <Column header="Actions" style="min-width: 11rem;">
      <template #body="{ data }">
        <!-- <Button icon="pi pi-eye" class="p-button-info m-1" @click="viewCard(data.id)" /> -->
        <Button icon="pi pi-pencil" class="p-button-warning m-1" @click="editCard(data.id)" />
        <Button icon="pi pi-trash" class="p-button-danger m-1" @click="deleteCard(data.id)" severity="danger" />
      </template>
    </Column>
    <Column field="title" header="Name" style="min-width: 10rem" sortable>
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
    <Column field="content" header="Content preview" style="min-width:auto;"></Column>
  </DataTable>
</template>
