<template>
  <div class="field">
    <label for="toggle">Select File Usage</label>
    <ToggleButton id="toggle" :onLabel="'Use entire file'" :offLabel="'Use partial file'" v-model="useEntireFile" style="width: 100%;" />
  </div>
  <div v-if="!useEntireFile" class="field">
    <label for="line">Select Line</label>
    <Dropdown id="line" v-model="selectedLine" :options="fileLines" placeholder="Select a line" style="width: 100%;" />
  </div>
</template>

<script setup>
const useEntireFile = ref(true);
const fileLines = ref<string[]>([]);
const selectedLine = ref<string | null>(null);

const api = inject<SpacedMdApiClient>('api');
if (!api) throw new Error('API client not provided');

// Watch for changes in the selected Markdown file and load its content lines.
watch(selectedMdFile, async (newVal) => {
  if (newVal && !useEntireFile.value) {
    try {
      const response = await api!.mdfile.getContent(newVal);
      if (response && response.content) {
        fileLines.value = response.content.split('\n');
      }
    } catch (error) {
      console.error("Failed to load file content lines:", error);
    }
  }
});

// Watch for toggle changes to reset the second dropdown if "Use partial file" is selected.
watch(useEntireFile, (newVal) => {
  if (newVal) {
    selectedLine.value = null;
  }
});
</script>
