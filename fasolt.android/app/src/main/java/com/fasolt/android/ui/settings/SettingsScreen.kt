package com.fasolt.android.ui.settings

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.HelpOutline
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
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
import com.fasolt.android.data.api.models.SnapshotDto
import com.fasolt.android.data.api.models.UserInfo
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
import java.util.Locale

private enum class SettingsSegment(val label: String) {
    Settings("Settings"),
    Snapshots("Snapshots"),
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(
    onOpenNotifications: () -> Unit,
    onOpenScheduling: () -> Unit,
    onOpenMcpSetup: () -> Unit,
    onOpenDeleteAccount: () -> Unit,
    onOpenHelp: () -> Unit = {},
    viewModel: SettingsViewModel = viewModel(),
    snapshotsViewModel: SnapshotsViewModel = viewModel(),
) {
    val state by viewModel.uiState.collectAsState()
    val snapshotsState by snapshotsViewModel.uiState.collectAsState()
    var showSignOutDialog by remember { mutableStateOf(false) }
    var selectedSegment by remember { mutableStateOf(SettingsSegment.Settings) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Settings") },
                actions = {
                    IconButton(onClick = onOpenHelp) {
                        Icon(
                            Icons.AutoMirrored.Filled.HelpOutline,
                            contentDescription = "Help",
                        )
                    }
                },
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding),
        ) {
            SingleChoiceSegmentedButtonRow(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp, vertical = 8.dp),
            ) {
                SettingsSegment.entries.forEachIndexed { index, segment ->
                    SegmentedButton(
                        selected = selectedSegment == segment,
                        onClick = { selectedSegment = segment },
                        shape = SegmentedButtonDefaults.itemShape(
                            index = index,
                            count = SettingsSegment.entries.size,
                        ),
                    ) {
                        Text(segment.label)
                    }
                }
            }

            when (selectedSegment) {
                SettingsSegment.Settings -> SettingsContent(
                    state = state,
                    onRetry = viewModel::load,
                    onSignOutClick = { showSignOutDialog = true },
                    onOpenNotifications = onOpenNotifications,
                    onOpenScheduling = onOpenScheduling,
                    onOpenMcpSetup = onOpenMcpSetup,
                    onOpenDeleteAccount = onOpenDeleteAccount,
                )
                SettingsSegment.Snapshots -> SnapshotsContent(
                    state = snapshotsState,
                    onCreate = snapshotsViewModel::createSnapshot,
                    onDismissResult = snapshotsViewModel::dismissCreateResult,
                )
            }
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
private fun SettingsContent(
    state: SettingsUiState,
    onRetry: () -> Unit,
    onSignOutClick: () -> Unit,
    onOpenNotifications: () -> Unit,
    onOpenScheduling: () -> Unit,
    onOpenMcpSetup: () -> Unit,
    onOpenDeleteAccount: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        AccountCard(
            isLoading = state.isLoading,
            user = state.user,
            serverUrl = state.serverUrl,
            errorMessage = state.errorMessage,
            onRetry = onRetry,
            onSignOutClick = onSignOutClick,
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

@Composable
private fun SnapshotsContent(
    state: SnapshotsUiState,
    onCreate: () -> Unit,
    onDismissResult: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        SectionCard {
            Column(Modifier.padding(16.dp)) {
                Text(
                    "Snapshots back up every card's content. The last 10 snapshots per deck are kept automatically. Restoring only reverts card content — your study progress is never affected. To restore, visit fasolt.app.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }

        SectionCard {
            Column(Modifier.padding(16.dp)) {
                Button(
                    onClick = onCreate,
                    enabled = !state.isCreating,
                    modifier = Modifier.fillMaxWidth(),
                ) {
                    if (state.isCreating) {
                        CircularProgressIndicator(
                            strokeWidth = 2.dp,
                            modifier = Modifier.height(20.dp),
                            color = MaterialTheme.colorScheme.onPrimary,
                        )
                        Spacer(Modifier.padding(start = 8.dp))
                    }
                    Text("Create Snapshot")
                }

                val result = state.createResult
                if (result != null) {
                    Spacer(Modifier.height(8.dp))
                    val message = if (result.created > 0) {
                        "Created snapshots for ${result.created} deck(s)."
                    } else {
                        "All decks unchanged — no snapshots created."
                    }
                    Text(
                        message,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.primary,
                    )
                    TextButton(onClick = onDismissResult) { Text("Dismiss") }
                }

                if (state.errorMessage != null) {
                    Spacer(Modifier.height(8.dp))
                    Text(
                        state.errorMessage,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.error,
                    )
                }
            }
        }

        SectionCard {
            Column(Modifier.padding(top = 8.dp, bottom = 8.dp)) {
                Text(
                    "History",
                    style = MaterialTheme.typography.titleSmall,
                    fontWeight = FontWeight.SemiBold,
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp),
                )

                when {
                    state.isLoading && state.snapshots.isEmpty() -> {
                        Box(
                            Modifier
                                .fillMaxWidth()
                                .padding(16.dp),
                            contentAlignment = Alignment.Center,
                        ) {
                            CircularProgressIndicator(strokeWidth = 2.dp, modifier = Modifier.height(20.dp))
                        }
                    }
                    state.snapshots.isEmpty() -> {
                        Text(
                            "No snapshots yet.",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp),
                        )
                    }
                    else -> {
                        LazyColumn(modifier = Modifier.fillMaxWidth().height((state.snapshots.size.coerceAtMost(10) * 64).dp)) {
                            items(state.snapshots, key = { it.id }) { snapshot ->
                                SnapshotRow(snapshot)
                                HorizontalDivider()
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun SnapshotRow(snapshot: SnapshotDto) {
    androidx.compose.foundation.layout.Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f)) {
            Text(
                snapshot.deckName ?: "Unknown deck",
                style = MaterialTheme.typography.bodyMedium,
            )
            Text(
                formatSnapshotDate(snapshot.createdAt),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Text(
            "${snapshot.cardCount} cards",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

private fun formatSnapshotDate(iso: String): String {
    return runCatching {
        val odt = OffsetDateTime.parse(iso).atZoneSameInstant(ZoneId.systemDefault())
        val formatter = DateTimeFormatter
            .ofLocalizedDateTime(FormatStyle.MEDIUM, FormatStyle.SHORT)
            .withLocale(Locale.getDefault())
        odt.format(formatter)
    }.getOrDefault(iso)
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
