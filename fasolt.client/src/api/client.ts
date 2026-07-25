const BASE_URL = '/api'

export interface ApiError {
  status: number
  errors: Record<string, string[]>
  /** Machine-readable code from endpoints that return `{ error, message }`. */
  code?: string
  /** Human-readable message that goes with `code`. */
  message?: string
}

export function isApiError(error: unknown): error is ApiError {
  return typeof error === 'object' && error !== null && 'status' in error && 'errors' in error
}

export async function apiFetch(path: string, options?: RequestInit): Promise<void>
export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T>
export async function apiFetch<T = void>(path: string, options?: RequestInit): Promise<T | void> {
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
    let code: string | undefined
    let message: string | undefined
    try {
      const body = await response.json()
      if (body.errors) {
        errors = body.errors
      }
      if (typeof body.error === 'string') code = body.error
      if (typeof body.message === 'string') message = body.message
    } catch {
      // No JSON body
    }
    throw { status: response.status, errors, code, message } as ApiError
  }

  const text = await response.text()
  if (!text) return

  return JSON.parse(text)
}

// Search types
export interface CardSearchResult {
  id: string
  headline: string
  state: string
}

export interface DeckSearchResult {
  id: string
  headline: string
  cardCount: number
}

export interface SearchResponse {
  cards: CardSearchResult[]
  decks: DeckSearchResult[]
}

export async function searchAll(query: string): Promise<SearchResponse> {
  return apiFetch<SearchResponse>(`/search?q=${encodeURIComponent(query)}`)
}

export interface PaginatedResponse<T> {
  items: T[]
  hasMore: boolean
  nextCursor: string | null
}

