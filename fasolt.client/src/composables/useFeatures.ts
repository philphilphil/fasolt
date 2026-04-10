import { ref } from 'vue'
import { apiFetch } from '@/api/client'

interface Features {
  githubLogin: boolean
  appleLogin: boolean
}

const features = ref<Features | null>(null)
let fetchPromise: Promise<void> | null = null

async function load() {
  try {
    const res = await apiFetch<{ features: Features }>('/health')
    features.value = res.features
  } catch {
    features.value = { githubLogin: false, appleLogin: false }
  }
}

export function useFeatures() {
  if (!fetchPromise) {
    fetchPromise = load()
  }
  return { features, ready: fetchPromise }
}
