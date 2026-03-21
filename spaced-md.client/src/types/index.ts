export interface Card {
  id: string
  fileId: string | null
  sourceHeading: string | null
  front: string
  back: string
  cardType: 'file' | 'section' | 'custom'
  createdAt: string
  easeFactor: number
  interval: number
  repetitions: number
  dueAt: string | null
  state: 'new' | 'learning' | 'mature'
}

export interface ExtractedContent {
  fronts: string[]
  back: string
}

export interface Stat {
  label: string
  value: string
  delta?: string
}

export interface MarkdownFile {
  id: string
  fileName: string
  sizeBytes: number
  uploadedAt: string
  cardCount: number
  headings: FileHeading[]
}

export interface FileHeading {
  level: number
  text: string
}

export interface FileDetail extends MarkdownFile {
  content: string
}

export interface BulkUploadResult {
  fileName: string
  success: boolean
  id: string | null
  error: string | null
}

export interface Group {
  id: string
  name: string
  cardCount: number
  dueCount: number
}

export type ReviewRating = 'again' | 'hard' | 'good' | 'easy'

// Placeholder — will be replaced with real API-backed type in Epic 4/6
export interface Deck {
  id: string
  name: string
  fileName: string
  cardCount: number
  dueCount: number
  nextReview: string
}

export interface FileUpdatePreview {
  fileId: string
  fileName: string
  updatedCards: { cardId: string; front: string; oldBack: string; newBack: string }[]
  orphanedCards: { cardId: string; front: string; sourceHeading: string }[]
  unchangedCount: number
  newSections: { heading: string; hasMarkers: boolean }[]
}

export interface DueCard {
  id: string
  front: string
  back: string
  cardType: string
  sourceHeading: string | null
  fileId: string | null
  state: string
  dueAt: string | null
}

export interface ReviewStats {
  dueCount: number
  totalCards: number
  studiedToday: number
}
