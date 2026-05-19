package com.fasolt.android.ui.cards

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.fasolt.android.FasoltApplication
import com.fasolt.android.data.api.models.CardDto
import com.fasolt.android.data.api.models.DeckDto
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface CardFormLoadState {
    data object Loading : CardFormLoadState
    data object Ready : CardFormLoadState
    data class Error(val message: String) : CardFormLoadState
}

/**
 * Backs [CardFormScreen] in both create and edit modes. Pass `cardId = null` for create,
 * or a non-null id to load + edit an existing card.
 *
 * The view layer drives all text-field state; this VM is responsible for:
 *  - bootstrapping (load card + decks for edit, just decks for create)
 *  - submit / delete
 *  - mapping errors to a single [error] stream the screen surfaces
 */
class CardFormViewModel(
    application: Application,
    val cardId: String?,
    val initialDeckId: String? = null,
) : AndroidViewModel(application) {
    private val app = application as FasoltApplication

    val isEdit: Boolean get() = cardId != null

    private val _loadState = MutableStateFlow<CardFormLoadState>(CardFormLoadState.Loading)
    val loadState: StateFlow<CardFormLoadState> = _loadState.asStateFlow()

    private val _decks = MutableStateFlow<List<DeckDto>>(emptyList())
    val decks: StateFlow<List<DeckDto>> = _decks.asStateFlow()

    private val _card = MutableStateFlow<CardDto?>(null)
    val card: StateFlow<CardDto?> = _card.asStateFlow()

    private val _isSaving = MutableStateFlow(false)
    val isSaving: StateFlow<Boolean> = _isSaving.asStateFlow()

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error.asStateFlow()

    init { bootstrap() }

    private fun bootstrap() {
        _loadState.value = CardFormLoadState.Loading
        viewModelScope.launch {
            runCatching {
                val decks = app.deckRepository.list()
                val card = cardId?.let { app.cardRepository.get(it) }
                decks to card
            }.onSuccess { (decks, card) ->
                _decks.value = decks
                _card.value = card
                _loadState.value = CardFormLoadState.Ready
            }.onFailure {
                _loadState.value = CardFormLoadState.Error(it.message ?: "Failed to load")
            }
        }
    }

    fun save(
        front: String,
        back: String,
        sourceFile: String?,
        deckIds: List<String>,
        onDone: (Boolean) -> Unit,
    ) {
        _isSaving.value = true
        _error.value = null
        viewModelScope.launch {
            val result = runCatching {
                if (cardId == null) {
                    // CreateCardRequest only accepts a single deckId. Pick the first if present;
                    // additional decks would need a follow-up update call, which we omit for now.
                    app.cardRepository.create(
                        front = front,
                        back = back,
                        sourceFile = sourceFile,
                        deckId = deckIds.firstOrNull() ?: initialDeckId,
                    )
                } else {
                    app.cardRepository.update(
                        id = cardId,
                        front = front,
                        back = back,
                        sourceFile = sourceFile,
                        deckIds = deckIds,
                    )
                }
            }
            _isSaving.value = false
            result.onSuccess { onDone(true) }
                .onFailure {
                    _error.value = it.message ?: "Failed to save card"
                    onDone(false)
                }
        }
    }

    fun delete(onDone: (Boolean) -> Unit) {
        val id = cardId ?: return onDone(false)
        _isSaving.value = true
        viewModelScope.launch {
            runCatching { app.cardRepository.delete(id) }
                .onSuccess {
                    _isSaving.value = false
                    onDone(true)
                }
                .onFailure {
                    _isSaving.value = false
                    _error.value = it.message ?: "Failed to delete card"
                    onDone(false)
                }
        }
    }

    fun clearError() { _error.value = null }
}
