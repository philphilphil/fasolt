<!-- filepath: /Users/phil/Projects/spaced-md/spaced-md.client/src/views/mdfiles/MdFiles.vue -->
<script setup lang="ts">
import { ref } from 'vue';
import MdFileUploadComponent from '@/components/mdfiles/MdFileUploadComponent.vue';
import MdFilesDataTableComponent from '@/components/mdfiles/MdFilesDataTableComponent.vue';

const dataTableRef = ref(null);

const refreshTable = () => {
  if (dataTableRef.value && dataTableRef.value.loadData) {
    dataTableRef.value.loadData();
  }
};
</script>

<template>
  <div>
    <MdFileUploadComponent @uploadComplete="refreshTable" />
    <MdFilesDataTableComponent ref="dataTableRef" />
  </div>
</template>
