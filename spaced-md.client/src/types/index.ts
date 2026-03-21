export interface Card {
  id: string
  deckId: string
  question: string
  answer: string
  sourceFile: string
  sourceSection: string | null
  dueAt: Date
  easeFactor: number
  interval: number
  repetitions: number
}

export interface Deck {
  id: string
  name: string
  fileName: string
  cardCount: number
  dueCount: number
  nextReview: string
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
