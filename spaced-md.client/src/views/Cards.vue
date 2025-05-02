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
const name = ref('');

const mdFiles = ref<MdFileResponse[]>([]);
const mdFile = ref<MdFileResponse>();

const toast = useToast();
const { tempSuccessToast, failToast } = useToastService(toast);

const selectedOption = ref('entire'); // 'entire' for entire file, 'partial' for partial file
const fileHeadings = ref<string[]>([]);
const selectedHeading = ref<string | null>(null);

const mdPreview = ref<string | null>(null);
const mdRaw = ref<string | null>(null);

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
    mdRaw.value = mdFile.value!.content!;
    mdPreview.value = await marked.parse(mdFile.value!.content!);
  } catch (error) {
    failToast("Error preparing card.");
  }
}

// Watch for changes in the selected Markdown file and call prepareCard.
watch(selectedMdFile, (selected) => {
  if (selected) {
    getMdDetails(selected);
  }
});

watch(selectedHeading, async (selected) => {
  if (selected) {
    const lines = mdFile.value!.content!.split('\n');
    const startIndex = lines.findIndex(line => line.trim() === selected);
    if (startIndex === -1) {
      mdPreview.value = null;
      return;
    }
    const headingMatch = selected.trim().match(/^(#+)/);
    const headingLevel = headingMatch ? headingMatch[1].length : 0;

    let content = lines[startIndex] + '\n';
    // Continue adding lines until a heading with equal or lower level is met
    for (let i = startIndex + 1; i < lines.length; i++) {
      const line = lines[i];
      if (line.trim().startsWith('#')) {
        const currentMatch = line.trim().match(/^(#+)/);
        const currentLevel = currentMatch ? currentMatch[1].length : 0;
        if (currentLevel <= headingLevel) break;
      }
      content += line + '\n';
    }
    mdRaw.value = content;
    mdPreview.value = await marked.parse(content);
  } else {
    mdPreview.value = null;
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
  const payload = {
    title: name.value,
    markdownFileId: selectedMdFile.value,
    content: mdRaw.value,
  };

  api!.card.post(payload).then(() => {
    tempSuccessToast("Card added successfully.");
  }).catch((error) => {
    if (error.additionalData) {
      failToast(error.additionalData.detail);
    } else {
      failToast("Error adding card.");
    }
  });
  closeDialog();
}

// Resets form fields and closes the dialog.
function closeDialog() {
  visible.value = false;
  selectedMdFile.value = null;
  comment.value = '';
  name.value = '';
  selectedOption.value = 'entire';
  selectedHeading.value = null;
  mdPreview.value = null;
  fileHeadings.value = [];
}
</script>

<template>
  <Button label="Add Card" icon="pi pi-plus" @click="visible = true" />

  <Dialog v-model:visible="visible" header="Add new card" :style="{ width: '60%', height: '60%' }">
    <div class="flex items-center gap-4 mb-4">
      <label for="name" class="font-semibold w-24">Name</label>
      <InputText v-model="name" id="name" class="w-80" autocomplete="off" />
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
      <div v-html="mdPreview" class="w-full h-110 overflow-auto border border-gray-300 rounded-md p-4"></div>
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
