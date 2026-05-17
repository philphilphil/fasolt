package com.fasolt.android.ui.settings

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.fasolt.android.FasoltApplication
import java.time.ZoneId
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/** Editable form state for FSRS scheduling. */
data class SchedulingSettingsUiState(
    val isLoading: Boolean = true,
    val isSaving: Boolean = false,
    val desiredRetention: Float = 0.90f,
    val maximumInterval: Int = 36500,
    val dayStartHour: Int = 4,
    val errorMessage: String? = null,
    val successMessage: String? = null,
)

class SchedulingSettingsViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application as FasoltApplication

    private val _uiState = MutableStateFlow(SchedulingSettingsUiState())
    val uiState: StateFlow<SchedulingSettingsUiState> = _uiState.asStateFlow()

    init { load() }

    fun load() {
        _uiState.value = _uiState.value.copy(isLoading = true, errorMessage = null, successMessage = null)
        viewModelScope.launch {
            runCatching { app.schedulingRepository.get() }
                .onSuccess { s ->
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        desiredRetention = s.desiredRetention.toFloat(),
                        maximumInterval = s.maximumInterval,
                        dayStartHour = s.dayStartHour,
                    )
                }
                .onFailure { error ->
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        errorMessage = error.message ?: "Could not load scheduling settings.",
                    )
                }
        }
    }

    fun setDesiredRetention(value: Float) {
        _uiState.value = _uiState.value.copy(desiredRetention = value, successMessage = null)
    }

    fun setMaximumInterval(value: Int) {
        _uiState.value = _uiState.value.copy(
            maximumInterval = value.coerceIn(1, 365),
            successMessage = null,
        )
    }

    fun setDayStartHour(value: Int) {
        _uiState.value = _uiState.value.copy(
            dayStartHour = value.coerceIn(0, 23),
            successMessage = null,
        )
    }

    fun save() {
        val s = _uiState.value
        _uiState.value = s.copy(isSaving = true, errorMessage = null, successMessage = null)
        viewModelScope.launch {
            runCatching {
                app.schedulingRepository.update(
                    desiredRetention = s.desiredRetention.toDouble(),
                    maximumInterval = s.maximumInterval,
                    dayStartHour = s.dayStartHour,
                    timeZone = ZoneId.systemDefault().id,
                )
            }
                .onSuccess { saved ->
                    _uiState.value = _uiState.value.copy(
                        isSaving = false,
                        desiredRetention = saved.desiredRetention.toFloat(),
                        maximumInterval = saved.maximumInterval,
                        dayStartHour = saved.dayStartHour,
                        successMessage = "Saved.",
                    )
                }
                .onFailure { error ->
                    _uiState.value = _uiState.value.copy(
                        isSaving = false,
                        errorMessage = error.message ?: "Could not save scheduling settings.",
                    )
                }
        }
    }
}
