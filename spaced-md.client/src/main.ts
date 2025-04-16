import { createApp } from 'vue';
import App from './App.vue';
import router from './router/index.ts';

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

import '@/assets/styles.scss';


const authProvider = new AnonymousAuthenticationProvider();
const adapter = new FetchRequestAdapter(authProvider);
adapter.baseUrl = "/api";
const client = createSpacedMdApiClient(adapter);

const app = createApp(App);

app.use(router);
app.use(PrimeVue, {
    theme: {
        preset: Aura,
        options: {
            darkModeSelector: '.app-dark'
        }
    }
});
app.use(ToastService);
app.use(ConfirmationService);
app.provide('api', client)
app.component('Toast', Toast);
app.component('Select', Select);
app.component('Fluid', Fluid);
app.component('DatePicker', DatePicker);
app.component('ToggleSwitch', ToggleSwitch);
app.directive('styleclass', StyleClass);
app.mount('#app');
