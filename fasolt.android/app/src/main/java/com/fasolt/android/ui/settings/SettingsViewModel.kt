package com.fasolt.android.ui.settings

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.fasolt.android.BuildConfig
import com.fasolt.android.FasoltApplication
import com.fasolt.android.data.api.models.UserInfo
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/** State for the top-level settings list. */
data class SettingsUiState(
    val isLoading: Boolean = true,
    val user: UserInfo? = null,
    val serverUrl: String? = null,
    val appVersion: String = BuildConfig.VERSION_NAME,
    val errorMessage: String? = null,
)

class SettingsViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application as FasoltApplication

    private val _uiState = MutableStateFlow(SettingsUiState())
    val uiState: StateFlow<SettingsUiState> = _uiState.asStateFlow()

    init { load() }

    fun load() {
        _uiState.value = _uiState.value.copy(isLoading = true, errorMessage = null)
        viewModelScope.launch {
            val serverUrl = app.secureStorage.serverUrl
            runCatching { app.accountRepository.me() }
                .onSuccess { user ->
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        user = user,
                        serverUrl = serverUrl,
                        errorMessage = null,
                    )
                }
                .onFailure { error ->
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        user = null,
                        serverUrl = serverUrl,
                        errorMessage = error.message ?: "Could not load account info.",
                    )
                }
        }
    }

    fun signOut() {
        app.authRepository.signOut()
    }
}
