package com.fasolt.android.ui.study

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.fasolt.android.FasoltApplication
import com.fasolt.android.data.api.models.DueCardDto
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface StudyUiState {
    data object Idle : StudyUiState
    data object Loading : StudyUiState
    data class Error(val message: String) : StudyUiState
    data class Studying(
        val cards: List<DueCardDto>,
        val currentIndex: Int,
        val isFlipped: Boolean,
        val isRating: Boolean,
        val ratingError: String? = null,
    ) : StudyUiState {
        val currentCard: DueCardDto? get() = cards.getOrNull(currentIndex)
        val totalCards: Int get() = cards.size
        val progress: Float
            get() = if (cards.isEmpty()) 0f else currentIndex.toFloat() / cards.size.toFloat()
    }

    data class Summary(
        val cardsStudied: Int,
        val ratingsCount: Map<String, Int>,
        val failedRatings: Int,
        val skippedCount: Int,
        val suspendedCount: Int,
    ) : StudyUiState
}

object StudyRatings {
    const val AGAIN = "again"
    const val HARD = "hard"
    const val GOOD = "good"
    const val EASY = "easy"
    val ALL = listOf(AGAIN, HARD, GOOD, EASY)
}

class StudyViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application as FasoltApplication

    private val _uiState = MutableStateFlow<StudyUiState>(StudyUiState.Idle)
    val uiState: StateFlow<StudyUiState> = _uiState.asStateFlow()

    private var cardsStudied = 0
    private var failedRatings = 0
    private var skippedCount = 0
    private var suspendedCount = 0

    /** True when the session has any rated or skipped cards — used by the X button to decide
     *  between showing the summary vs. dismissing outright (matches iOS behaviour). */
    val hasProgress: Boolean get() = cardsStudied > 0 || skippedCount > 0
    private val ratingsCount = mutableMapOf(
        StudyRatings.AGAIN to 0,
        StudyRatings.HARD to 0,
        StudyRatings.GOOD to 0,
        StudyRatings.EASY to 0,
    )

    fun startSession(deckId: String?) {
        _uiState.value = StudyUiState.Loading
        resetTallies()
        viewModelScope.launch {
            runCatching { app.reviewRepository.due(deckId, 50) }
                .onSuccess { cards ->
                    if (cards.isEmpty()) {
                        _uiState.value = summaryState()
                    } else {
                        _uiState.value = StudyUiState.Studying(
                            cards = cards,
                            currentIndex = 0,
                            isFlipped = false,
                            isRating = false,
                        )
                    }
                }
                .onFailure {
                    _uiState.value = StudyUiState.Error(
                        it.message ?: "Could not load cards. Check your connection.",
                    )
                }
        }
    }

    fun flip() {
        val current = _uiState.value as? StudyUiState.Studying ?: return
        if (!current.isFlipped) {
            _uiState.value = current.copy(isFlipped = true)
        }
    }

    fun rate(rating: String) {
        val current = _uiState.value as? StudyUiState.Studying ?: return
        if (current.isRating || !current.isFlipped) return
        val card = current.currentCard ?: return

        ratingsCount[rating] = (ratingsCount[rating] ?: 0) + 1
        cardsStudied += 1
        val nextIndex = current.currentIndex + 1
        if (nextIndex >= current.cards.size) {
            _uiState.value = summaryState()
        } else {
            _uiState.value = current.copy(
                currentIndex = nextIndex,
                isFlipped = false,
                isRating = true,
                ratingError = null,
            )
        }

        viewModelScope.launch {
            runCatching { app.reviewRepository.rate(card.id, rating) }
                .onFailure {
                    failedRatings += 1
                    val s = _uiState.value
                    if (s is StudyUiState.Studying) {
                        _uiState.value = s.copy(
                            isRating = false,
                            ratingError = "Rating may not have been saved.",
                        )
                    } else if (s is StudyUiState.Summary) {
                        _uiState.value = s.copy(failedRatings = failedRatings)
                    }
                }
                .onSuccess {
                    val s = _uiState.value
                    if (s is StudyUiState.Studying) {
                        _uiState.value = s.copy(isRating = false)
                    }
                }
        }
    }

    fun skipCard() {
        val current = _uiState.value as? StudyUiState.Studying ?: return
        skippedCount += 1
        val nextIndex = current.currentIndex + 1
        if (nextIndex >= current.cards.size) {
            _uiState.value = summaryState()
        } else {
            _uiState.value = current.copy(
                currentIndex = nextIndex,
                isFlipped = false,
                ratingError = null,
            )
        }
    }

    fun suspendCard() {
        val current = _uiState.value as? StudyUiState.Studying ?: return
        val card = current.currentCard ?: return

        suspendedCount += 1
        val nextIndex = current.currentIndex + 1
        if (nextIndex >= current.cards.size) {
            _uiState.value = summaryState()
        } else {
            _uiState.value = current.copy(
                currentIndex = nextIndex,
                isFlipped = false,
                ratingError = null,
            )
        }

        viewModelScope.launch {
            runCatching { app.reviewRepository.setCardSuspended(card.id, true) }
        }
    }

    fun endSessionEarly() {
        if (_uiState.value !is StudyUiState.Studying) return
        _uiState.value = summaryState()
    }

    fun studyAgain(deckId: String?) = startSession(deckId)

    private fun summaryState() = StudyUiState.Summary(
        cardsStudied = cardsStudied,
        ratingsCount = ratingsCount.toMap(),
        failedRatings = failedRatings,
        skippedCount = skippedCount,
        suspendedCount = suspendedCount,
    )

    private fun resetTallies() {
        cardsStudied = 0
        failedRatings = 0
        skippedCount = 0
        suspendedCount = 0
        StudyRatings.ALL.forEach { ratingsCount[it] = 0 }
    }
}
