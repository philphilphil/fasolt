package com.fasolt.android.ui.settings

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.compose.foundation.clickable
import androidx.compose.foundation.shape.RoundedCornerShape
import com.fasolt.android.data.api.models.UserInfo

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(
    onOpenNotifications: () -> Unit,
    onOpenScheduling: () -> Unit,
    onOpenMcpSetup: () -> Unit,
    onOpenDeleteAccount: () -> Unit,
    viewModel: SettingsViewModel = viewModel(),
) {
    val state by viewModel.uiState.collectAsState()
    var showSignOutDialog by remember { mutableStateOf(false) }

    Scaffold(
        topBar = { TopAppBar(title = { Text("Settings") }) },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            AccountCard(
                isLoading = state.isLoading,
                user = state.user,
                serverUrl = state.serverUrl,
                errorMessage = state.errorMessage,
                onRetry = viewModel::load,
                onSignOutClick = { showSignOutDialog = true },
                onDeleteAccountClick = onOpenDeleteAccount,
            )

            SectionCard {
                SettingsRow(
                    title = "Notifications",
                    subtitle = "How often Fasolt checks for due cards",
                    onClick = onOpenNotifications,
                )
                HorizontalDivider()
                SettingsRow(
                    title = "Scheduling (FSRS)",
                    subtitle = "Retention, max interval, day start",
                    onClick = onOpenScheduling,
                )
                HorizontalDivider()
                SettingsRow(
                    title = "MCP setup",
                    subtitle = "Connect your AI agent",
                    onClick = onOpenMcpSetup,
                )
            }

            AboutCard(version = state.appVersion)
        }
    }

    if (showSignOutDialog) {
        AlertDialog(
            onDismissRequest = { showSignOutDialog = false },
            title = { Text("Sign out?") },
            text = { Text("You'll need to sign in again to use Fasolt.") },
            confirmButton = {
                TextButton(onClick = {
                    showSignOutDialog = false
                    viewModel.signOut()
                }) { Text("Sign out") }
            },
            dismissButton = {
                TextButton(onClick = { showSignOutDialog = false }) { Text("Cancel") }
            },
        )
    }
}

@Composable
private fun AccountCard(
    isLoading: Boolean,
    user: UserInfo?,
    serverUrl: String?,
    errorMessage: String?,
    onRetry: () -> Unit,
    onSignOutClick: () -> Unit,
    onDeleteAccountClick: () -> Unit,
) {
    SectionCard {
        Column(Modifier.padding(16.dp)) {
            Text("Account", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
            Spacer(Modifier.height(8.dp))

            when {
                isLoading -> {
                    Box(Modifier.fillMaxWidth().padding(vertical = 8.dp), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator(strokeWidth = 2.dp, modifier = Modifier.height(20.dp))
                    }
                }
                errorMessage != null && user == null -> {
                    Text(
                        errorMessage,
                        color = MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.bodySmall,
                    )
                    TextButton(onClick = onRetry) { Text("Retry") }
                }
                user != null -> {
                    LabelValueRow(
                        label = if (user.displayName != null) "Signed in as" else "Email",
                        value = user.displayName ?: user.email,
                    )
                    LabelValueRow(
                        label = "Account type",
                        value = user.externalProvider ?: "Email & password",
                    )
                    if (user.isAdmin) {
                        Spacer(Modifier.height(4.dp))
                        Text(
                            "Admin",
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.primary,
                        )
                    }
                    if (!serverUrl.isNullOrBlank()) {
                        LabelValueRow(label = "Server", value = serverUrl)
                    }
                }
            }
        }

        HorizontalDivider()

        SettingsRow(
            title = "Sign out",
            leadingIcon = {
                Icon(Icons.AutoMirrored.Filled.Logout, contentDescription = null)
            },
            onClick = onSignOutClick,
        )
        HorizontalDivider()
        SettingsRow(
            title = "Delete account",
            subtitle = "Permanently deletes your account and data",
            titleColor = MaterialTheme.colorScheme.error,
            onClick = onDeleteAccountClick,
        )
    }
}

@Composable
private fun AboutCard(version: String) {
    SectionCard {
        Column(Modifier.padding(16.dp)) {
            Text("About", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
            Spacer(Modifier.height(8.dp))
            LabelValueRow(label = "Version", value = version)
        }
    }
}

@Composable
private fun LabelValueRow(label: String, value: String) {
    Column(Modifier.fillMaxWidth().padding(vertical = 4.dp)) {
        Text(label, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(value, style = MaterialTheme.typography.bodyMedium)
    }
}

@Composable
private fun SectionCard(content: @Composable () -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
    ) {
        Column { content() }
    }
}

@Composable
private fun SettingsRow(
    title: String,
    subtitle: String? = null,
    titleColor: androidx.compose.ui.graphics.Color = MaterialTheme.colorScheme.onSurface,
    leadingIcon: (@Composable () -> Unit)? = null,
    onClick: () -> Unit,
) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(0.dp))
            .clickable(onClick = onClick),
        color = MaterialTheme.colorScheme.surface,
    ) {
        androidx.compose.foundation.layout.Row(
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 14.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            if (leadingIcon != null) {
                leadingIcon()
                Spacer(Modifier.height(0.dp))
                Spacer(Modifier.padding(start = 12.dp))
            }
            Column(Modifier.weight(1f)) {
                Text(title, style = MaterialTheme.typography.bodyLarge, color = titleColor)
                if (subtitle != null) {
                    Text(
                        subtitle,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
            Icon(
                Icons.AutoMirrored.Filled.KeyboardArrowRight,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}
