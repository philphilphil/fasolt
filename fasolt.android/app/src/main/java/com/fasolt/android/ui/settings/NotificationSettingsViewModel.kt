package com.fasolt.android.ui.settings

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.fasolt.android.FasoltApplication
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * Interval picker for push notification cadence.
 *
 * "Off" is encoded as [INTERVAL_OFF] (== 0). The server treats 0 as "do not
 * send" — the iOS app stays in 4..24 because it always wants reminders, but
 * Android exposes Off so users can disable without revoking system permission.
 */
data class NotificationSettingsUiState(
    val isLoading: Boolean = true,
    val isSaving: Boolean = false,
    val intervalHours: Int = DEFAULT_INTERVAL,
    val hasDeviceToken: Boolean = false,
    val errorMessage: String? = null,
)

const val INTERVAL_OFF = 0
const val DEFAULT_INTERVAL = 4
val ALLOWED_INTERVALS: List<Int> = listOf(INTERVAL_OFF, 1, 4, 12, 24)

class NotificationSettingsViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application as FasoltApplication

    private val _uiState = MutableStateFlow(NotificationSettingsUiState())
    val uiState: StateFlow<NotificationSettingsUiState> = _uiState.asStateFlow()

    init { load() }

    fun load() {
        _uiState.value = _uiState.value.copy(isLoading = true, errorMessage = null)
        viewModelScope.launch {
            runCatching { app.notificationRepository.settings() }
                .onSuccess { s ->
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        intervalHours = s.intervalHours,
                        hasDeviceToken = s.hasDeviceToken,
                        errorMessage = null,
                    )
                }
                .onFailure { error ->
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        errorMessage = error.message ?: "Could not load notification settings.",
                    )
                }
        }
    }

    fun updateInterval(hours: Int) {
        val previous = _uiState.value.intervalHours
        _uiState.value = _uiState.value.copy(isSaving = true, intervalHours = hours, errorMessage = null)
        viewModelScope.launch {
            runCatching { app.notificationRepository.updateSettings(hours) }
                .onSuccess { s ->
                    _uiState.value = _uiState.value.copy(
                        isSaving = false,
                        intervalHours = s.intervalHours,
                        hasDeviceToken = s.hasDeviceToken,
                    )
                }
                .onFailure { error ->
                    _uiState.value = _uiState.value.copy(
                        isSaving = false,
                        intervalHours = previous,
                        errorMessage = error.message ?: "Could not update notification interval.",
                    )
                }
        }
    }
}
