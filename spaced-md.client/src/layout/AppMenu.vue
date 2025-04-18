<script setup>
import { ref } from 'vue';

import AppMenuItem from './AppMenuItem.vue';

const model = ref([
  {
    label: 'Home',
    items: [
      { label: 'Dashboard', icon: 'pi pi-fw pi-home', to: '/home' },
      { label: 'Study', icon: 'pi pi-fw pi-chart-line', to: '/uikit/button'  }]
  },
  {
    label: 'Manage',
    items: [
      { label: 'Files', icon: 'pi pi-fw pi-hashtag', to: '/mdfiles/list' },
      { label: 'Cards', icon: 'pi pi-fw pi-book', to: '/uikit/input' },
    ]
  },
  {
    label: 'Misc',
    items: [
      {
        label: 'Documentation',
        icon: 'pi pi-fw pi-book',
        to: '/documentation'
      },
      {
        label: 'View Source',
        icon: 'pi pi-fw pi-github',
        url: 'https://github.com/philphilphil/spaced-md',
        target: '_blank'
      }
    ]
  }
]);
</script>

<template>
  <ul class="layout-menu">
    <template v-for="(item, i) in model" :key="item">
      <app-menu-item v-if="!item.separator" :item="item" :index="i"></app-menu-item>
      <li v-if="item.separator" class="menu-separator"></li>
    </template>
  </ul>
</template>

<style lang="scss" scoped></style>
