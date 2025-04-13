import './assets/main.css'
import PrimeVue from 'primevue/config';
import DatePicker from 'primevue/datepicker';
import Aura from '@primeuix/themes/aura';
import { createApp } from 'vue'
import App from './App.vue'

const app = createApp(App);
app.use(PrimeVue, {
  theme: {
      preset: Aura
  }
});
app.component('DatePicker', DatePicker);


app.mount('#app');
