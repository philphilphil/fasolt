package com.fasolt.android.ui.dashboard

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.fasolt.android.FasoltApplication
import com.fasolt.android.data.api.models.Overview
import com.fasolt.android.data.api.models.StudyStats
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class DashboardData(
    val overview: Overview,
    val studyStats: StudyStats,
)

sealed interface DashboardUiState {
    data object Loading : DashboardUiState
    data class Loaded(val data: DashboardData) : DashboardUiState
    data class Error(val message: String) : DashboardUiState
}

class DashboardViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application as FasoltApplication

    private val _uiState = MutableStateFlow<DashboardUiState>(DashboardUiState.Loading)
    val uiState: StateFlow<DashboardUiState> = _uiState.asStateFlow()

    init { refresh() }

    fun refresh() {
        // Don't drop currently-loaded data on refresh — keep showing it.
        if (_uiState.value !is DashboardUiState.Loaded) {
            _uiState.value = DashboardUiState.Loading
        }
        viewModelScope.launch {
            // coroutineScope { } contains the async children — without it, an exception
            // from either call would propagate to viewModelScope and crash the app
            // before runCatching could see it.
            runCatching {
                coroutineScope {
                    val overviewDeferred = async { app.reviewRepository.overview() }
                    val statsDeferred = async { app.reviewRepository.studyStats() }
                    DashboardData(overviewDeferred.await(), statsDeferred.await())
                }
            }
                .onSuccess { _uiState.value = DashboardUiState.Loaded(it) }
                .onFailure {
                    _uiState.value = DashboardUiState.Error(it.message ?: "Failed to load dashboard")
                }
        }
    }
}
