export interface Card {
  id: string
  sourceFile: string | null
  sourceHeading: string | null
  front: string
  back: string
  createdAt: string
  stability: number | null
  difficulty: number | null
  dueAt: string | null
  state: 'new' | 'learning' | 'review' | 'relearning'
  decks: { id: string; name: string }[]
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
}

export interface DueCard {
  id: string
  front: string
  back: string
  sourceFile: string | null
  sourceHeading: string | null
  state: string
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
