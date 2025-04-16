import { createApp } from 'vue';
import { Client } from './api/apiClient.ts';
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


import '@/assets/styles.scss';


const app = createApp(App);

const api: Client = new Client("http://localhost:5041");

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
app.provide('api', api)
app.component('Toast', Toast);
app.component('Select', Select);
app.component('Fluid', Fluid);
app.component('DatePicker', DatePicker);
app.component('ToggleSwitch', ToggleSwitch);
app.directive('styleclass', StyleClass);
app.mount('#app');
