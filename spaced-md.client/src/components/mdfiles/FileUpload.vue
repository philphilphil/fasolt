<script setup lang="ts">
import type { SpacedMdApiClient } from '@/api/spacedMdApiClient';
import { ref, inject } from 'vue';

const api = inject<SpacedMdApiClient>('api');

const isUploading = ref(false);
const message = ref('');

const handleFileUpload = (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (!target.files || target.files.length === 0) return;
  const file = target.files[0];
  const reader = new FileReader();

  reader.onload = async (e: ProgressEvent<FileReader>) => {
    const content = e.target?.result;
    if (typeof content === 'string' && api) {
      try {
        isUploading.value = true;
        var MdFileUploadRequest = {
          name: file.name,
          content: content,
        };
        await api.mdfile.put(MdFileUploadRequest);
        message.value = 'File uploaded successfully!';
      } catch (error) {
        message.value = 'Error uploading file.';
      } finally {
        isUploading.value = false;
      }
    }
  };

  reader.readAsText(file);
};
</script>

<template>
  <div>
    <h2>Upload File</h2>
    <input type="file" @change="handleFileUpload" />
    <p v-if="isUploading">Uploading...</p>
    <p v-if="message">{{ message }}</p>
  </div>
</template>

<style scoped>
/* Add your styles here */
</style>
