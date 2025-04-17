import { createApp } from 'vue';
import App from './App.vue';
import router from './router/index.ts';
import { createPinia } from 'pinia'
import Aura from '@primeuix/themes/aura';
import PrimeVue from 'primevue/config';
import ConfirmationService from 'primevue/confirmationservice';
import ToastService from 'primevue/toastservice';
import StyleClass from 'primevue/styleclass';
import Toast from 'primevue/toast';
import Select from 'primevue/select';
import Fluid from 'primevue/fluid';
import ToggleSwitch  from 'primevue/toggleswitch';
import DatePicker from 'primevue/datepicker';
import { AnonymousAuthenticationProvider } from "@microsoft/kiota-abstractions";
import { FetchRequestAdapter } from "@microsoft/kiota-http-fetchlibrary";
import { createSpacedMdApiClient } from './api/spacedMdApiClient.ts';
import { definePreset } from '@primeuix/themes';

import '@/assets/styles.scss';
const pinia = createPinia()

const authProvider = new AnonymousAuthenticationProvider();
const adapter = new FetchRequestAdapter(authProvider);
adapter.baseUrl = "/api";
const client = createSpacedMdApiClient(adapter);

const app = createApp(App);

const spacedPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50:  '#fff8f3',
      100: '#ffe7d8',
      200: '#ffd0ad',
      300: '#ffb079',
      400: '#ff944b',
      500: '#f88232',
      600: '#db6517',
      700: '#b44f12',
      800: '#8f3d11',
      900: '#733210',
      950: '#401b08',
    }
  }
});

app.use(router);
app.use(PrimeVue, {
    theme: {
        preset: spacedPreset,
        options: {
            darkModeSelector: '.app-dark'
        }
    }
});
app.use(ToastService);
app.use(ConfirmationService);
app.use(pinia);
app.provide('api', client)
app.component('Toast', Toast);
app.component('Select', Select);
app.component('Fluid', Fluid);
app.component('DatePicker', DatePicker);
app.component('ToggleSwitch', ToggleSwitch);
app.directive('styleclass', StyleClass);
app.mount('#app');
