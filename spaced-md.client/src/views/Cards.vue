<script setup lang="ts">
import { ref, onMounted, watch, inject } from 'vue';
import Button from 'primevue/button';
import Dialog from 'primevue/dialog';
import Dropdown from 'primevue/dropdown';
import Textarea from 'primevue/textarea';
import ToggleButton from 'primevue/togglebutton';
import { useToastService } from '@/service/toastService';
import { useToast } from 'primevue';
import type { SpacedMdApiClient } from '@/api/spacedMdApiClient';
import type { MdFileResponse } from '@/api/models';
import { marked } from 'marked';

const api = inject<SpacedMdApiClient>('api');
if (!api) throw new Error('API client not provided');

const visible = ref(false);
const selectedMdFile = ref<string | null>(null);
const comment = ref('');

const mdFiles = ref<MdFileResponse[]>([]);
const mdFile = ref<MdFileResponse>();

const toast = useToast();
const { tempSuccessToast, failToast } = useToastService(toast);

const selectedOption = ref('entire'); // 'entire' for entire file, 'partial' for partial file
const fileHeadings = ref<string[]>([]);
const selectedHeading = ref<string | null>(null);

const mdPreview = ref<string | null>(null);

async function loadMdFiles() {
  try {
    const result = await api!.mdfile.get();
    mdFiles.value = result || [];
  } catch (error) {
    failToast("Failed to load md files:");
  }
}

onMounted(() => {
  loadMdFiles();
});

// This method is called after the user selects a Markdown file.
async function getMdDetails(mdFileId: string) {
  try {
    const result = await api!.mdfile.byId(mdFileId).get();
    mdFile.value = result;
    mdPreview.value = await marked.parse(mdFile.value!.content!);
  } catch (error) {
    failToast("Error preparing card.");
  }
}

// Watch for changes in the selected Markdown file and call prepareCard.
watch(selectedMdFile, (newVal) => {
  if (newVal) {
    getMdDetails(newVal);
  }
});

// Fetch lines of the selected Markdown file when "Use partial file" is selected.
watch([selectedMdFile, selectedOption], async ([newMdFile, option]) => {
  if (newMdFile && option === 'partial' && mdFile.value && mdFile.value.content) {
    const lines = mdFile.value.content.split('\n');
    fileHeadings.value = lines.filter(line => line.trim().startsWith('#'));
  } else {
    fileHeadings.value = [];
  }
});

// Function to call when user clicks "Add" button.
async function addCard() {
  if (!selectedMdFile.value || (selectedOption.value === 'partial' && !selectedHeading.value)) {
    failToast("Please complete all required fields.");
    return;
  }
  try {
    const payload = {
      title: "New Card",
      content: selectedOption.value === 'entire' ? comment.value : selectedHeading.value,
      markdownFileId: selectedMdFile.value
    };

    const response = await fetch('/card', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      throw new Error("Failed to add card");
    }
    tempSuccessToast("Card added successfully.");
    closeDialog();
  } catch (error) {
    console.error(error);
    failToast("Error adding card.");
  }
}

// Resets form fields and closes the dialog.
function closeDialog() {
  visible.value = false;
  selectedMdFile.value = null;
  comment.value = '';
}
</script>

<template>
  <Button label="Add Card" icon="pi pi-plus" @click="visible = true" />

  <Dialog v-model:visible="visible" header="Add new card" :style="{ width: '60%', height: '60%' }">
    <div class="flex items-center gap-4 mb-4">
      <label for="name" class="font-semibold w-24">Name</label>
      <InputText id="name" class="w-80" autocomplete="off" />
    </div>
    <div class="flex items-center gap-4 mb-8">
      <label for="mdfile" class="font-semibold w-24">Md file</label>
      <Dropdown id="mdfile" v-model="selectedMdFile" :options="mdFiles" optionLabel="fileName" optionValue="id"
        placeholder="Select a file" class="w-80" />
    </div>
    <div class="flex items-center gap-4 mb-8">
      <label class="font-semibold w-24">File Usage</label>
      <RadioButton id="entire" value="entire" v-model="selectedOption" />
      <label for="entire">Use entire file</label>
      <RadioButton id="partial" value="partial" v-model="selectedOption" />
      <label for="partial">Use partial file</label>
    </div>
    <div v-if="selectedOption === 'partial'" class="flex items-center gap-4 mb-8">
      <label for="line" class="font-semibold w-24">Heading</label>
      <Dropdown id="line" v-model="selectedHeading" :options="fileHeadings" placeholder="Select a heading" />
    </div>
    <div class="flex items-center gap-4 mb-8">
      <label for="comment" class="font-semibold w-24">Comment</label>
      <Textarea id="comment" v-model="comment" rows="3" placeholder="Enter your comment" class="w-80" />
    </div>
    <div class="gap-4 mb-8">
      <span class="text-2xl font-semibold">Preview</span>
      <div v-html="mdPreview" class="w-full h-120 overflow-auto border border-gray-300 rounded-md p-4"></div>
    </div>
    <div class="flex justify-end gap-2">
      <Button label="Cancel" icon="pi pi-times" class="p-button-text" @click="closeDialog" />
      <Button label="Save" icon="pi pi-check" @click="addCard" autoFocus />
    </div>
  </Dialog>
</template>

<style scoped>
.field {
  margin-bottom: 1rem;
}
</style>
