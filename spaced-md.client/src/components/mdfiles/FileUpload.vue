<script setup lang="ts">
import { Client, MdFileUploadRequest } from '@/api/apiClient';
import { ref } from 'vue';
import { inject } from 'vue';

const api = inject<Client>('api');
if (!api) throw new Error('API client not provided');

const selectedFile = ref<File | null>(null);
const fileContent = ref<string>('');
const errorMessage = ref<string>('');
const successMessage = ref<string>('');

function handleFileChange(event: Event) {
  const target = event.target as HTMLInputElement;
  if (target.files && target.files.length > 0) {
    const file = target.files[0];
    // Only allow Markdown files
    if (file.type === "text/markdown" || file.name.endsWith(".md")) {
      selectedFile.value = file;
      errorMessage.value = '';
    } else {
      errorMessage.value = 'Please select a markdown (.md) file.';
      selectedFile.value = null;
    }
  }
}

async function uploadFile() {
  if (!selectedFile.value) {
    errorMessage.value = 'No file selected.';
    return;
  }

  const reader = new FileReader();
  reader.onload = async (e: ProgressEvent<FileReader>) => {
    const content = e.target?.result;
    if (typeof content === 'string') {
      fileContent.value = content;
      try {
        api?.putMdfile(
          new MdFileUploadRequest({
            name: selectedFile.value?.name,
            content: fileContent.value,
          })
        );
        successMessage.value = 'File uploaded successfully!';
      } catch (error) {
        errorMessage.value = 'File upload failed.';
        console.error("Upload failed:", error);
      }
    } else {
      errorMessage.value = 'Unable to read file.';
    }
  };
  reader.onerror = () => {
    errorMessage.value = 'Error reading file.';
  };
  reader.readAsText(selectedFile.value);
}
</script>

<template>
  <div>
    <input type="file" accept=".md" @change="handleFileChange" />
    <button @click="uploadFile">Upload File</button>
    <div v-if="errorMessage" style="color: red;">{{ errorMessage }}</div>
    <div v-if="successMessage" style="color: green;">{{ successMessage }}</div>
  </div>
</template>

<style scoped>
/* Styling for file input area - adjust as needed */
</style>
