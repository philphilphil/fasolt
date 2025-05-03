<!-- filepath: /Users/phil/Projects/spaced-md/spaced-md.client/src/components/UploadComponent.vue -->
<script setup lang="ts">
import { defineEmits, inject } from 'vue';
import type { SpacedMdApiClient } from '@/api/spacedMdApiClient';
import { useToastService } from '@/service/toastService';
import { useToast } from 'primevue';

const emit = defineEmits<{
  (e: 'uploadComplete'): void;
}>();

const api = inject<SpacedMdApiClient>('api');
if (!api) throw new Error('API client not provided');

const toast = useToast();
const { tempSuccessToast, failToast } = useToastService(toast);

const onUpload = async (event: any) => {
  const files = Array.isArray(event.files) ? event.files : [event.files];

  const uploadPromises = files.map((file: File) => {
    return new Promise<void>((resolve) => {
      const reader = new FileReader();
      reader.onload = async (e: ProgressEvent<FileReader>) => {
        const content = e.target?.result;
        try {
          const MdFileUploadRequest = {
            name: file.name,
            content: String(content),
          };
          await api.mdfile.put(MdFileUploadRequest);
          await tempSuccessToast(`File ${file.name} uploaded.`);
        } catch (error) {
          await failToast(`Failed ${file.name} to upload file.`);
        }
        resolve();
      };
      reader.readAsText(file);
    });
  });

  await Promise.all(uploadPromises);
    emit('uploadComplete');
};
</script>

<template>
  <div style="display: flex; justify-content: flex-start; margin-bottom: 1em;">
    <FileUpload mode="basic" :auto="true" :customUpload="true" @uploader="onUpload" chooseLabel="Upload Markdown File"
      :multiple="true" accept=".md,text/markdown" invalidFileTypeMessage="Only .md allowed."/>
  </div>
</template>
