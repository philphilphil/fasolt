package com.fasolt.android.ui.decks

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.fasolt.android.FasoltApplication
import com.fasolt.android.data.api.models.DeckDto
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface DecksUiState {
    data object Loading : DecksUiState
    data class Loaded(val decks: List<DeckDto>) : DecksUiState
    data class Error(val message: String) : DecksUiState
}

class DecksViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application as FasoltApplication

    private val _uiState = MutableStateFlow<DecksUiState>(DecksUiState.Loading)
    val uiState: StateFlow<DecksUiState> = _uiState.asStateFlow()

    private val _createError = MutableStateFlow<String?>(null)
    val createError: StateFlow<String?> = _createError.asStateFlow()

    init { refresh() }

    fun refresh() {
        _uiState.value = DecksUiState.Loading
        viewModelScope.launch {
            runCatching { app.deckRepository.list() }
                .onSuccess { _uiState.value = DecksUiState.Loaded(it) }
                .onFailure { _uiState.value = DecksUiState.Error(it.message ?: "Failed to load decks") }
        }
    }

    fun signOut() {
        app.authRepository.signOut()
    }

    /**
     * Creates a new deck. Invokes [onDone] with `true` if the call succeeded, `false` otherwise.
     * On success the deck list is refreshed so the new deck appears.
     */
    fun createDeck(name: String, description: String?, onDone: (Boolean) -> Unit) {
        viewModelScope.launch {
            _createError.value = null
            runCatching { app.deckRepository.create(name, description) }
                .onSuccess {
                    onDone(true)
                    refresh()
                }
                .onFailure {
                    _createError.value = it.message ?: "Failed to create deck"
                    onDone(false)
                }
        }
    }

    fun clearCreateError() {
        _createError.value = null
    }
}
