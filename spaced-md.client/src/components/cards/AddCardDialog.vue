<script setup lang="ts">
import { ref, onMounted, watch, inject, defineProps, defineEmits } from 'vue';
import Button from 'primevue/button';
import Dialog from 'primevue/dialog';
import Dropdown from 'primevue/dropdown';
import InputText from 'primevue/inputtext';
import { useToastService } from '@/service/toastService';
import { useToast } from 'primevue';
import type { SpacedMdApiClient } from '@/api/spacedMdApiClient';
import type { CardUsageType, MdFileResponse } from '@/api/models';
import { useMarkdownService } from '@/service/mdService';

const props = defineProps({
  visible: {
    type: Boolean,
    default: false
  },
  cardId: {
    type: String,
    default: ""
  }
});
const emit = defineEmits(['update:visible', 'refreshCards']);

function updateVisible(val: boolean) {
  emit('update:visible', val);
}

const toast = useToast();
const { tempSuccessToast, failToast } = useToastService(toast);
const { mdToHtml, getSectionContent } = useMarkdownService();

const api = inject<SpacedMdApiClient>('api');
if (!api) throw new Error('API client not provided');

const mdFiles = ref<MdFileResponse[]>([]);
const mdFile = ref<MdFileResponse | null>(null);

const selectedMdFile = ref<string | null>(null);
const name = ref('');
const selectedOption = ref<CardUsageType>('EntireFile');
const fileHeadings = ref<string[]>([]);
const selectedHeading = ref<string | null>(null);
const mdPreview = ref<string | null>(null);
const mdRaw = ref<string | null>(null);

async function loadMdFiles() {
  try {
    const result = await api!.mdfiles.get();
    mdFiles.value = result || [];
  } catch (error) {
    failToast("Failed to load md files:");
  }
}

watch(() => props.visible, (vis) => {
  if (vis) {
    loadMdFiles();
  }
});

// Initialize fields when in edit mode
watch(() => props.cardId, (editCardId) => {
  if (editCardId) {
    api!.cards.byId(editCardId).get().then((card) => {
      name.value = card!.title || '';
      selectedMdFile.value = card!.mdFileId || null;
      selectedOption.value = card!.usageType!;
      selectedHeading.value = card!.heading || null;
    }).catch((error) => {
      failToast("Error loading card.");
    });

  } else {
    name.value = '';
    selectedMdFile.value = null;
    selectedOption.value = 'EntireFile';
    selectedHeading.value = null;
    mdPreview.value = null;
    fileHeadings.value = [];
  }
}, { immediate: true });

async function loadMdFileDetails(mdFileId: string) {
  try {
    const result = await api!.mdfiles.byId(mdFileId).get();
    mdFile.value = result!;
    mdRaw.value = mdFile.value!.content!;
    mdPreview.value = await mdToHtml(mdFile.value!.content!);
    fileHeadings.value = mdFile.value?.headings!.map((heading) => heading.heading!) || [];
  } catch (error) {
    failToast("Error preparing card.");
  }
}

watch(selectedMdFile, (selected) => {
  if (selected) {
    loadMdFileDetails(selected);
  }
});

watch(selectedHeading, async (selected) => {
  if (selected && mdFile.value) {
    const content = await getSectionContent(mdFile.value.content!, selected);
    mdRaw.value = content;
    mdPreview.value = await mdToHtml(content);
  }
});

async function saveCard() {
  if (!selectedMdFile.value || (selectedOption.value === 'PartialFile' && !selectedHeading.value)) {
    failToast("Please complete all required fields.");
    return;
  }

  const payload = {
    title: name.value,
    mdFileId: selectedMdFile.value,
    content: mdRaw.value,
    usageType: selectedOption.value,
    heading: selectedOption.value === 'PartialFile' ? selectedHeading.value : null,
  };

  if (props.cardId) {
    // Edit mode – update existing card.
    // api!.cards.byId(props.cardId).update(payload).then(() => {
    //   tempSuccessToast("Card updated successfully.");
    //   emit('refreshCards'); // Inform parent to refresh the data table.
    // }).catch((error) => {
    //   if (error.additionalData) {
    //     failToast(error.additionalData.detail);
    //   } else {
    //     failToast("Error updating card.");
    //   }
    // });
  } else {
    api!.card.post(payload).then(() => {
      tempSuccessToast("Card added successfully.");
      emit('refreshCards');
    }).catch((error) => {
      if (error.additionalData) {
        failToast(error.additionalData.detail);
      } else {
        failToast("Error adding card.");
      }
    });
  }
  closeDialog();
}

function closeDialog() {
  updateVisible(false);
  // Reset the fields after closing.
  selectedMdFile.value = null;
  name.value = '';
  selectedOption.value = "EntireFile";
  selectedHeading.value = null;
  mdPreview.value = null;
  fileHeadings.value = [];
}
</script>

<template>
  <Dialog v-model:visible="props.visible" header="Card" :style="{ width: '60%', height: '60%' }">
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
      <RadioButton id="entire" value="EntireFile" v-model="selectedOption" />
      <label for="entire">Use entire file</label>
      <RadioButton id="partial" value="PartialFile" v-model="selectedOption" />
      <label for="partial">Use partial file</label>
    </div>
    <div v-if="selectedOption == 'PartialFile'" class="flex items-center gap-4 mb-8">
      <label for="line" class="font-semibold w-24">Heading</label>
      <Dropdown id="line" v-model="selectedHeading" :options="fileHeadings" placeholder="Select a heading" />
    </div>
    <div class="flex justify-end gap-2">
      <Button label="Cancel" icon="pi pi-times" class="p-button-text" @click="closeDialog" />
      <Button label="Save" icon="pi pi-check" @click="saveCard" autoFocus />
    </div>
    <div class="gap-4 mb-8 mt-4">
      <span class="text-2xl font-semibold">Preview</span>
      <div v-html="mdPreview" class="w-full h-110 overflow-auto border border-gray-300 rounded-md p-4"></div>
    </div>
  </Dialog>
</template>

<style scoped>
.field {
  margin-bottom: 1rem;
}
</style>
