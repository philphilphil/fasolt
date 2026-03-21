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

// API Token types
export interface CreateTokenRequest {
  name: string
  expiresAt?: string | null
}

export interface CreateTokenResponse {
  id: string
  name: string
  token: string
  createdAt: string
  expiresAt: string | null
}

export interface TokenListItem {
  id: string
  name: string
  tokenPrefix: string
  createdAt: string
  lastUsedAt: string | null
  expiresAt: string | null
  isExpired: boolean
  isRevoked: boolean
}

// API Token functions
export async function createApiToken(request: CreateTokenRequest): Promise<CreateTokenResponse> {
  return apiFetch<CreateTokenResponse>('/tokens', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export async function listApiTokens(): Promise<TokenListItem[]> {
  return apiFetch<TokenListItem[]>('/tokens')
}

export async function revokeApiToken(id: string): Promise<void> {
  await apiFetch(`/tokens/${id}`, { method: 'DELETE' })
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

