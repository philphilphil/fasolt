package com.fasolt.android.ui.settings

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.fasolt.android.FasoltApplication
import com.fasolt.android.data.api.models.CreateSnapshotResponse
import com.fasolt.android.data.api.models.SnapshotDto
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class SnapshotsUiState(
    val snapshots: List<SnapshotDto> = emptyList(),
    val isLoading: Boolean = false,
    val isCreating: Boolean = false,
    val errorMessage: String? = null,
    val createResult: CreateSnapshotResponse? = null,
)

class SnapshotsViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application as FasoltApplication

    private val _uiState = MutableStateFlow(SnapshotsUiState())
    val uiState: StateFlow<SnapshotsUiState> = _uiState.asStateFlow()

    init { load() }

    fun load() {
        _uiState.value = _uiState.value.copy(isLoading = true, errorMessage = null)
        viewModelScope.launch {
            runCatching { app.snapshotRepository.recent() }
                .onSuccess { list ->
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        snapshots = list,
                        errorMessage = null,
                    )
                }
                .onFailure { error ->
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        errorMessage = error.message ?: "Could not load snapshots.",
                    )
                }
        }
    }

    fun createSnapshot() {
        _uiState.value = _uiState.value.copy(isCreating = true, errorMessage = null, createResult = null)
        viewModelScope.launch {
            runCatching { app.snapshotRepository.create() }
                .onSuccess { result ->
                    _uiState.value = _uiState.value.copy(
                        isCreating = false,
                        createResult = result,
                    )
                    load()
                }
                .onFailure { error ->
                    _uiState.value = _uiState.value.copy(
                        isCreating = false,
                        errorMessage = error.message ?: "Could not create snapshot.",
                    )
                }
        }
    }

    fun dismissCreateResult() {
        _uiState.value = _uiState.value.copy(createResult = null)
    }
}
