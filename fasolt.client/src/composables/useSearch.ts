import { ref, computed, watch, type Ref } from 'vue'
import { useRouter } from 'vue-router'
import { searchAll, type SearchResponse, type CardSearchResult, type DeckSearchResult } from '@/api/client'

export type SearchItem =
  | { type: 'card'; data: CardSearchResult }
  | { type: 'deck'; data: DeckSearchResult }

export function useSearch() {
  const router = useRouter()
  const query = ref('')
  const results = ref<SearchResponse>({ cards: [], decks: [] })
  const isLoading = ref(false)
  const isOpen = ref(false)
  const activeIndex = ref(0)
  const error = ref<string | null>(null)

  let debounceTimer: ReturnType<typeof setTimeout> | null = null

  const flatItems = computed<SearchItem[]>(() => {
    const items: SearchItem[] = []
    for (const deck of results.value.decks) items.push({ type: 'deck', data: deck })
    for (const card of results.value.cards) items.push({ type: 'card', data: card })
    return items
  })

  const totalResults = computed(() => flatItems.value.length)

  const hasResults = computed(() =>
    results.value.cards.length > 0 ||
    results.value.decks.length > 0
  )

  watch(query, (val) => {
    if (debounceTimer) clearTimeout(debounceTimer)
    const trimmed = val.trim()
    if (trimmed.length < 2) {
      results.value = { cards: [], decks: [] }
      isOpen.value = false
      return
    }
    isOpen.value = true
    debounceTimer = setTimeout(async () => {
      isLoading.value = true
      error.value = null
      try {
        results.value = await searchAll(trimmed)
        activeIndex.value = 0
      } catch (e) {
        results.value = { cards: [], decks: [] }
        error.value = 'Search failed'
      } finally {
        isLoading.value = false
      }
    }, 300)
  })

  function navigateToResult(item: SearchItem) {
    switch (item.type) {
      case 'card':
        router.push(`/cards/${item.data.id}`)
        break
      case 'deck':
        router.push(`/decks/${item.data.id}`)
        break
    }
    close()
  }

  function onKeyDown(e: KeyboardEvent) {
    if (!isOpen.value) return

    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault()
        activeIndex.value = Math.min(activeIndex.value + 1, flatItems.value.length - 1)
        break
      case 'ArrowUp':
        e.preventDefault()
        activeIndex.value = Math.max(activeIndex.value - 1, 0)
        break
      case 'Enter':
        e.preventDefault()
        if (flatItems.value[activeIndex.value]) {
          navigateToResult(flatItems.value[activeIndex.value])
        }
        break
      case 'Escape':
        e.preventDefault()
        close()
        break
    }
  }

  function close() {
    isOpen.value = false
    query.value = ''
    results.value = { cards: [], decks: [] }
    activeIndex.value = 0
    error.value = null
  }

  function open(inputRef: Ref<HTMLInputElement | null>) {
    isOpen.value = true
    inputRef.value?.focus()
  }

  return {
    query,
    results,
    isLoading,
    isOpen,
    activeIndex,
    error,
    flatItems,
    totalResults,
    hasResults,
    onKeyDown,
    navigateToResult,
    close,
    open,
  }
}
