package com.fasolt.android.ui.cards

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.fasolt.android.FasoltApplication
import com.fasolt.android.data.api.models.CardDto
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * Paginated list of cards. Filters by deck and/or source file are immutable for the lifetime of
 * the VM — change them by recreating the VM (different `key` to `viewModel(key = …)`).
 */
class CardListViewModel(
    application: Application,
    val deckIdFilter: String? = null,
    val sourceFileFilter: String? = null,
) : AndroidViewModel(application) {
    private val app = application as FasoltApplication

    private val _cards = MutableStateFlow<List<CardDto>>(emptyList())
    val cards: StateFlow<List<CardDto>> = _cards.asStateFlow()

    private val _isLoading = MutableStateFlow(false)
    val isLoading: StateFlow<Boolean> = _isLoading.asStateFlow()

    private val _isLoadingMore = MutableStateFlow(false)
    val isLoadingMore: StateFlow<Boolean> = _isLoadingMore.asStateFlow()

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error.asStateFlow()

    private var nextCursor: String? = null
    private var hasMore: Boolean = true

    init { refresh() }

    fun refresh() {
        _isLoading.value = true
        _error.value = null
        nextCursor = null
        hasMore = true
        viewModelScope.launch {
            runCatching {
                app.cardRepository.list(
                    deckId = deckIdFilter,
                    sourceFile = sourceFileFilter,
                    cursor = null,
                    limit = PAGE_SIZE,
                )
            }.onSuccess { page ->
                _cards.value = page.items
                nextCursor = page.nextCursor
                hasMore = page.hasMore
            }.onFailure {
                _error.value = it.message ?: "Failed to load cards"
            }
            _isLoading.value = false
        }
    }

    /** Loads the next page if [hasMore]. Called as the list scrolls toward the end. */
    fun loadMoreIfNeeded() {
        if (!hasMore || _isLoadingMore.value || _isLoading.value) return
        val cursor = nextCursor ?: return
        _isLoadingMore.value = true
        viewModelScope.launch {
            runCatching {
                app.cardRepository.list(
                    deckId = deckIdFilter,
                    sourceFile = sourceFileFilter,
                    cursor = cursor,
                    limit = PAGE_SIZE,
                )
            }.onSuccess { page ->
                _cards.value = _cards.value + page.items
                nextCursor = page.nextCursor
                hasMore = page.hasMore
            }.onFailure {
                _error.value = it.message ?: "Failed to load more cards"
            }
            _isLoadingMore.value = false
        }
    }

    private companion object { const val PAGE_SIZE = 50 }
}
