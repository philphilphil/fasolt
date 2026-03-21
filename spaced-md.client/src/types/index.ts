export interface Card {
  id: string
  fileId: string | null
  sourceHeading: string | null
  front: string
  back: string
  cardType: 'file' | 'section' | 'custom'
  createdAt: string
}

export interface ExtractedContent {
  front: string
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
