<script setup lang="ts">
import type { MdFileResponse } from '@/api/models';
import type { SpacedMdApiClient } from '@/api/spacedMdApiClient';
import DataTable from 'primevue/datatable';
import { ref, onMounted } from 'vue'
import { inject } from 'vue';
import { FilterMatchMode } from '@primevue/core/api';
import { useToastService } from '@/service/toastService';
import { useToast } from 'primevue';

const api = inject<SpacedMdApiClient>('api');
if (!api) { throw new Error('API client not found'); }

const toast = useToast()
const { tempSuccessToast, failToast } = useToastService(toast);

var mdFiles = ref<MdFileResponse[]>([]);

onMounted(async () => {
  await loadMdFiles();
});

async function loadMdFiles() {
  try {
    const result = await api!.mdfile.get();
    mdFiles.value = result || [];
  } catch (error) {
    console.error("Failed to fetch weather forecast:", error);
  }
}

const onUpload = async (event: any) => {
  const files = Array.isArray(event.files) ? event.files : [event.files];

  // ai wrote this i just hope its fine
  const uploadPromises = files.map((file: File) => {
    return new Promise<void>((resolve) => {
      const reader = new FileReader();
      reader.onload = async (e: ProgressEvent<FileReader>) => {
        const content = e.target?.result;
        try {
          const MdFileUploadRequest = {
            name: file.name,
            content: String(content)
          };
          await api!.mdfile.put(MdFileUploadRequest);
          tempSuccessToast('File ' + file.name + ' uploaded.');
        } catch (error) {
          failToast('Failed ' + file.name + ' to upload file.');
        }
        resolve();
      };
      reader.readAsText(file);
    });
  });

  await Promise.all(uploadPromises);

  await loadMdFiles();
};

const filters = ref({
  global: { value: null, matchMode: FilterMatchMode.CONTAINS },
  fileName: { value: null, matchMode: FilterMatchMode.CONTAINS }
})

</script>

<template>
  <div style="display: flex; justify-content: flex-start; margin-bottom: 1em;">
    <FileUpload mode="basic" :auto="true" :customUpload="true" @uploader="onUpload" chooseLabel="Upload Markdown File"
      :multiple="true" accept=".md,text/markdown" invalidFileTypeMessage="Only .md allowed."/>
    </div>

  <DataTable :value="mdFiles" paginator :rows="20" :rowsPerPageOptions="[5, 10, 20, 50]" tableStyle="min-width: 50rem"
    v-model:filters="filters" filterDisplay="row"
    paginatorTemplate="FirstPageLink PrevPageLink PageLinks NextPageLink LastPageLink RowsPerPageDropdown CurrentPageReport"
    currentPageReportTemplate="Total: {totalRecords}">
    <!-- <Column field="id" header="Id" style="width: 25%"></Column> -->

    <Column field="fileName" header="Filename" style="width: 25%" sortable>
      <template #filter="{ filterModel, filterCallback }">
        <InputText v-model="filterModel.value" placeholder="Filter by filename" @input="filterCallback()" />
      </template>
    </Column>

    <Column field="content" header="Content" style="width: 50%"></Column>
  </DataTable>
</template>

<style scoped></style>
