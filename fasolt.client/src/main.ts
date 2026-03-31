import { createApp } from 'vue'
import { createPinia } from 'pinia'
import * as Sentry from '@sentry/vue'
import App from './App.vue'
import router from './router'
import './style.css'

const app = createApp(App)

const bugsinkDsn = import.meta.env.VITE_BUGSINK_DSN
if (bugsinkDsn) {
  Sentry.init({
    app,
    dsn: bugsinkDsn,
    environment: import.meta.env.MODE,
    integrations: [
      Sentry.browserTracingIntegration({ router }),
    ],
  })
}

app.use(createPinia())
app.use(router)
app.mount('#app')
