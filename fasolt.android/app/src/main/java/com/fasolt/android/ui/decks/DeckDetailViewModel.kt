package com.fasolt.android.ui.decks

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.fasolt.android.FasoltApplication
import com.fasolt.android.data.api.models.DeckDetailDto
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface DeckDetailUiState {
    data object Loading : DeckDetailUiState
    data class Loaded(val detail: DeckDetailDto) : DeckDetailUiState
    data class Error(val message: String) : DeckDetailUiState
}

/**
 * Backs [DeckDetailScreen]. The screen is responsible for navigation; this VM only
 * owns the detail payload + a transient error stream for write actions.
 *
 * Constructed via the explicit factory below so we can capture the [deckId] route arg
 * without ferrying it through SavedStateHandle (we don't have Hilt yet — see CLAUDE.md).
 */
class DeckDetailViewModel(
    application: Application,
    private val deckId: String,
) : AndroidViewModel(application) {
    private val app = application as FasoltApplication

    private val _uiState = MutableStateFlow<DeckDetailUiState>(DeckDetailUiState.Loading)
    val uiState: StateFlow<DeckDetailUiState> = _uiState.asStateFlow()

    private val _actionError = MutableStateFlow<String?>(null)
    val actionError: StateFlow<String?> = _actionError.asStateFlow()

    init { load() }

    fun load() {
        _uiState.value = DeckDetailUiState.Loading
        viewModelScope.launch {
            runCatching { app.deckRepository.get(deckId) }
                .onSuccess { _uiState.value = DeckDetailUiState.Loaded(it) }
                .onFailure { _uiState.value = DeckDetailUiState.Error(it.message ?: "Failed to load deck") }
        }
    }

    fun toggleSuspended() {
        val current = (_uiState.value as? DeckDetailUiState.Loaded)?.detail ?: return
        viewModelScope.launch {
            runCatching { app.deckRepository.setSuspended(current.id, !current.isSuspended) }
                .onSuccess { load() }
                .onFailure { _actionError.value = it.message ?: "Failed to update deck" }
        }
    }

    fun updateDeck(name: String, description: String?, onDone: (Boolean) -> Unit) {
        viewModelScope.launch {
            runCatching { app.deckRepository.update(deckId, name, description) }
                .onSuccess { onDone(true); load() }
                .onFailure {
                    _actionError.value = it.message ?: "Failed to update deck"
                    onDone(false)
                }
        }
    }

    fun deleteDeck(deleteCards: Boolean, onDone: (Boolean) -> Unit) {
        viewModelScope.launch {
            runCatching { app.deckRepository.delete(deckId, deleteCards) }
                .onSuccess { onDone(true) }
                .onFailure {
                    _actionError.value = it.message ?: "Failed to delete deck"
                    onDone(false)
                }
        }
    }

    fun clearActionError() { _actionError.value = null }
}
