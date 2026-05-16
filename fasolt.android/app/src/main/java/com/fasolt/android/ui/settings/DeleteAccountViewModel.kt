package com.fasolt.android.ui.settings

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.fasolt.android.FasoltApplication
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class DeleteAccountUiState(
    val confirmationText: String = "",
    val isDeleting: Boolean = false,
    val errorMessage: String? = null,
    val isDeleted: Boolean = false,
) {
    val canDelete: Boolean
        get() = confirmationText.trim().equals("DELETE", ignoreCase = false) && !isDeleting
}

class DeleteAccountViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application as FasoltApplication

    private val _uiState = MutableStateFlow(DeleteAccountUiState())
    val uiState: StateFlow<DeleteAccountUiState> = _uiState.asStateFlow()

    fun setConfirmationText(value: String) {
        _uiState.value = _uiState.value.copy(confirmationText = value, errorMessage = null)
    }

    fun deleteAccount() {
        val s = _uiState.value
        if (!s.canDelete) return
        _uiState.value = s.copy(isDeleting = true, errorMessage = null)
        viewModelScope.launch {
            runCatching { app.accountRepository.deleteAccount() }
                .onSuccess {
                    app.authRepository.signOut()
                    _uiState.value = _uiState.value.copy(isDeleting = false, isDeleted = true)
                }
                .onFailure { error ->
                    _uiState.value = _uiState.value.copy(
                        isDeleting = false,
                        errorMessage = error.message ?: "Could not delete account. Please try again.",
                    )
                }
        }
    }
}
