export interface Card {
  id: string
  sourceFile: string | null
  front: string
  back: string
  frontSvg: string | null
  backSvg: string | null
  createdAt: string
  stability: number | null
  difficulty: number | null
  step: number | null
  dueAt: string | null
  state: 'new' | 'learning' | 'review' | 'relearning'
  lastReviewedAt: string | null
  isSuspended: boolean
  decks: { id: string; name: string; isSuspended: boolean }[]
}

export interface Stat {
  label: string
  value: string
  delta?: string
}

export type ReviewRating = 'again' | 'hard' | 'good' | 'easy'

export type DeckVisibility = 'private' | 'unlisted' | 'public'

export interface Deck {
  id: string
  name: string
  description: string | null
  cardCount: number
  dueCount: number
  createdAt: string
  isSuspended: boolean
  visibility: DeckVisibility
  publishedAt: string | null
  copyCount: number
  copiedFromDeckPublicId: string | null
  copiedFromHandle: string | null
}

export interface DeckDetail extends Deck {
  cards: DeckCard[]
}

export interface DeckCard {
  id: string
  front: string
  back: string
  sourceFile: string | null
  state: string
  dueAt: string | null
  isSuspended: boolean
  frontSvg: string | null
  backSvg: string | null
}

export interface DueCard {
  id: string
  front: string
  back: string
  sourceFile: string | null
  state: string
  frontSvg: string | null
  backSvg: string | null
}

export interface ReviewStats {
  dueCount: number
  totalCards: number
  studiedToday: number
}

export interface StudyStats {
  currentStreak: number
  bestStreak: number
  totalAnswered: number
  answeredToday: number
}

export interface DailyActivity {
  date: string
  count: number
  hadDue: boolean
}

export interface RatingMix {
  again: number
  hard: number
  good: number
  easy: number
}

export interface Progress {
  currentStreak: number
  bestStreak: number
  totalAnswered: number
  answeredToday: number
  answeredThisWeek: number
  answeredThisMonth: number
  dailyActivity: DailyActivity[]
  ratingMix: RatingMix
}

export interface SourceItem {
  sourceFile: string
  cardCount: number
  dueCount: number
}

/** A public/unlisted deck as served by the anonymous library API. */
export interface LibraryDeck {
  id: string
  name: string
  description: string | null
  authorHandle: string | null
  cardCount: number
  copyCount: number
  publishedAt: string | null
}

export interface LibraryListResponse {
  items: LibraryDeck[]
  totalCount: number
  page: number
  pageSize: number
}

export interface LibrarySampleCard {
  front: string
  back: string
  frontSvg: string | null
  backSvg: string | null
}

export interface LibraryDeckDetail extends LibraryDeck {
  visibility: DeckVisibility
  sampleCards: LibrarySampleCard[]
}

export type LibrarySort = 'popular' | 'recent'

/** Account-level publishing identity. */
export interface HandleInfo {
  handle: string | null
  canPublish: boolean
}

export interface DeckSnapshot {
  id: string
  deckName: string | null
  cardCount: number
  createdAt: string
  contentChanges: number | null
}

export interface SnapshotDiff {
  deleted: DiffDeletedCard[]
  modified: DiffModifiedCard[]
  added: DiffAddedCard[]
}

export interface DiffDeletedCard {
  cardId: string
  front: string
  back: string
  sourceFile: string | null
  stability: number | null
  dueAt: string | null
  stillExists: boolean
}

export interface DiffModifiedCard {
  cardId: string
  front: string
  currentFront: string
  back: string
  currentBack: string
  snapshotFrontSvg: string | null
  currentFrontSvg: string | null
  snapshotBackSvg: string | null
  currentBackSvg: string | null
}

export interface DiffAddedCard {
  cardId: string
  front: string
  back: string
}
