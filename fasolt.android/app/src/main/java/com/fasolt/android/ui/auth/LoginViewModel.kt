package com.fasolt.android.ui.auth

import android.app.Application
import android.content.Intent
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.fasolt.android.FasoltApplication
import com.fasolt.android.data.api.FasoltApiFactory
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class LoginUiState(
    val serverUrl: String = FasoltApiFactory.DEFAULT_SERVER_URL,
    val isLoading: Boolean = false,
    val error: String? = null,
)

class LoginViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application as FasoltApplication
    private val auth = app.authRepository

    private val _uiState = MutableStateFlow(LoginUiState())
    val uiState: StateFlow<LoginUiState> = _uiState.asStateFlow()

    val isAuthenticated: StateFlow<Boolean> = auth.isAuthenticated

    fun setServerUrl(value: String) {
        _uiState.value = _uiState.value.copy(serverUrl = value, error = null)
    }

    /** Builds the AppAuth intent for the configured server URL. */
    fun buildLoginIntent(): Intent {
        _uiState.value = _uiState.value.copy(isLoading = true, error = null)
        return auth.buildAuthorizationIntent(_uiState.value.serverUrl)
    }

    fun onAuthorizationResult(data: Intent?) {
        viewModelScope.launch {
            val result = auth.completeAuthorization(data)
            _uiState.value = _uiState.value.copy(
                isLoading = false,
                error = result.exceptionOrNull()?.message,
            )
        }
    }

    fun cancelLoading() {
        _uiState.value = _uiState.value.copy(isLoading = false)
    }
}
