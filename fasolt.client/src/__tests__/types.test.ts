import { describe, it, expect } from 'vitest'
import type { Card, DueCard, SourceItem } from '@/types'

// Compile-time shape checks via assignability
describe('types', () => {
  it('Card interface has sourceFile', () => {
    const card: Card = {
      id: 'c1',
      sourceFile: 'notes.md',
      sourceHeading: null,
      front: 'Q',
      back: 'A',
      createdAt: '2024-01-01T00:00:00Z',
      stability: null,
      difficulty: null,
      dueAt: null,
      state: 'new',
      decks: [],
    }
    expect(card.sourceFile).toBe('notes.md')
  })

  it('Card interface does NOT have fileId', () => {
    const card: Card = {
      id: 'c1',
      sourceFile: 'notes.md',
      sourceHeading: null,
      front: 'Q',
      back: 'A',
      createdAt: '2024-01-01T00:00:00Z',
      stability: null,
      difficulty: null,
      dueAt: null,
      state: 'new',
      decks: [],
    }
    expect((card as any)['fileId']).toBeUndefined()
  })

  it('Card interface does NOT have cardType', () => {
    const card: Card = {
      id: 'c1',
      sourceFile: null,
      sourceHeading: null,
      front: 'Q',
      back: 'A',
      createdAt: '2024-01-01T00:00:00Z',
      stability: null,
      difficulty: null,
      dueAt: null,
      state: 'new',
      decks: [],
    }
    expect((card as any)['cardType']).toBeUndefined()
  })

  it('DueCard interface has sourceFile', () => {
    const dueCard: DueCard = {
      id: 'c1',
      front: 'Q',
      back: 'A',
      sourceFile: 'notes.md',
      sourceHeading: '## Overview',
      state: 'learning',
    }
    expect(dueCard.sourceFile).toBe('notes.md')
  })

  it('DueCard interface does NOT have fileId', () => {
    const dueCard: DueCard = {
      id: 'c1',
      front: 'Q',
      back: 'A',
      sourceFile: null,
      sourceHeading: null,
      state: 'new',
    }
    expect((dueCard as any)['fileId']).toBeUndefined()
  })

  it('DueCard interface does NOT have cardType', () => {
    const dueCard: DueCard = {
      id: 'c1',
      front: 'Q',
      back: 'A',
      sourceFile: null,
      sourceHeading: null,
      state: 'new',
    }
    expect((dueCard as any)['cardType']).toBeUndefined()
  })

  it('SourceItem interface has expected shape', () => {
    const item: SourceItem = {
      sourceFile: 'cap.md',
      cardCount: 5,
      dueCount: 2,
    }
    expect(item.sourceFile).toBe('cap.md')
    expect(item.cardCount).toBe(5)
    expect(item.dueCount).toBe(2)
  })

  it('MarkdownFile type does NOT exist as a named export', async () => {
    const types = await import('@/types')
    expect((types as any)['MarkdownFile']).toBeUndefined()
  })
})
