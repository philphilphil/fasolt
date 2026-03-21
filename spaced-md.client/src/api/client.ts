const BASE_URL = '/api'

export interface ApiError {
  status: number
  errors: Record<string, string[]>
}

export function isApiError(error: unknown): error is ApiError {
  return typeof error === 'object' && error !== null && 'status' in error && 'errors' in error
}

export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
    ...options,
  })

  if (!response.ok) {
    let errors: Record<string, string[]> = {}
    try {
      const body = await response.json()
      if (body.errors) {
        errors = body.errors
      }
    } catch {
      // No JSON body
    }
    throw { status: response.status, errors } as ApiError
  }

  const text = await response.text()
  if (!text) return undefined as T

  return JSON.parse(text)
}

// Search types
export interface CardSearchResult {
  id: string
  headline: string
  cardType: string
  state: string
}

export interface DeckSearchResult {
  id: string
  headline: string
  cardCount: number
}

export interface FileSearchResult {
  id: string
  headline: string
}

export interface SearchResponse {
  cards: CardSearchResult[]
  decks: DeckSearchResult[]
  files: FileSearchResult[]
}

export async function searchAll(query: string): Promise<SearchResponse> {
  return apiFetch<SearchResponse>(`/search?q=${encodeURIComponent(query)}`)
}

export async function apiUpload<T>(path: string, formData: FormData): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    credentials: 'include',
    body: formData,
  })

  if (!response.ok) {
    let errors: Record<string, string[]> = {}
    try {
      const body = await response.json()
      if (body.errors) {
        errors = body.errors
      }
    } catch {
      // No JSON body
    }
    throw { status: response.status, errors } as ApiError
  }

  const text = await response.text()
  if (!text) return undefined as T

  return JSON.parse(text)
}
