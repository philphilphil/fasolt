package com.fasolt.android.ui.progress

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.fasolt.android.FasoltApplication
import com.fasolt.android.data.api.models.ProgressDto
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface ProgressUiState {
    data object Loading : ProgressUiState
    data class Loaded(val progress: ProgressDto) : ProgressUiState
    data class Error(val message: String) : ProgressUiState
}

class ProgressViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application as FasoltApplication

    private val _uiState = MutableStateFlow<ProgressUiState>(ProgressUiState.Loading)
    val uiState: StateFlow<ProgressUiState> = _uiState.asStateFlow()

    init { refresh() }

    fun refresh() {
        if (_uiState.value !is ProgressUiState.Loaded) {
            _uiState.value = ProgressUiState.Loading
        }
        viewModelScope.launch {
            runCatching { app.reviewRepository.progress() }
                .onSuccess { _uiState.value = ProgressUiState.Loaded(it) }
                .onFailure {
                    _uiState.value = ProgressUiState.Error(it.message ?: "Failed to load progress")
                }
        }
    }
}
