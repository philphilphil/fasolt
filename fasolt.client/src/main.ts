import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { logger } from './lib/axiom'
import './style.css'

const app = createApp(App)

if (logger) {
  const activeLogger = logger
  app.config.errorHandler = (err, instance, info) => {
    activeLogger.error('Vue error', {
      error: err instanceof Error ? { message: err.message, stack: err.stack } : String(err),
      info,
      component: instance?.$options?.name ?? 'unknown',
      url: window.location.href,
    })
  }

  window.addEventListener('unhandledrejection', (event) => {
    activeLogger.error('Unhandled promise rejection', {
      reason: event.reason instanceof Error
        ? { message: event.reason.message, stack: event.reason.stack }
        : String(event.reason),
      url: window.location.href,
    })
  })
}

app.use(createPinia())
app.use(router)
app.mount('#app')
