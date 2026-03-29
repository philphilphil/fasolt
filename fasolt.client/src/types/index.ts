export interface Card {
  id: string
  sourceFile: string | null
  sourceHeading: string | null
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

export interface Deck {
  id: string
  name: string
  description: string | null
  cardCount: number
  dueCount: number
  createdAt: string
  isSuspended: boolean
}

export interface DeckDetail extends Deck {
  cards: DeckCard[]
}

export interface DeckCard {
  id: string
  front: string
  back: string
  sourceFile: string | null
  sourceHeading: string | null
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
  sourceHeading: string | null
  state: string
  frontSvg: string | null
  backSvg: string | null
}

export interface ReviewStats {
  dueCount: number
  totalCards: number
  studiedToday: number
}

export interface SourceItem {
  sourceFile: string
  cardCount: number
  dueCount: number
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
  snapshotStability: number | null
  currentStability: number | null
  hasContentChanges: boolean
  hasFsrsChanges: boolean
}

export interface DiffAddedCard {
  cardId: string
  front: string
  back: string
}
