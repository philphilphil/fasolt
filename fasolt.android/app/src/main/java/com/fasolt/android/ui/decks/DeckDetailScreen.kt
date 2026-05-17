package com.fasolt.android.ui.decks

import android.app.Application
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.widget.Toast
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.PauseCircle
import androidx.compose.material.icons.filled.PlayCircle
import androidx.compose.material.icons.outlined.ContentCopy
import androidx.compose.material.icons.outlined.Inbox
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.VerticalDivider
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewmodel.compose.viewModel
import com.fasolt.android.FasoltApplication
import com.fasolt.android.data.api.models.DeckCardDto
import com.fasolt.android.ui.decks.components.DeckFormSheet

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DeckDetailScreen(
    deckId: String,
    onNavigateBack: () -> Unit,
    onCardClick: (String) -> Unit,
) {
    val context = LocalContext.current
    val app = context.applicationContext as FasoltApplication
    val viewModel: DeckDetailViewModel = viewModel(
        key = "deck-detail-$deckId",
        factory = object : ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : ViewModel> create(modelClass: Class<T>): T =
                DeckDetailViewModel(app as Application, deckId) as T
        },
    )

    val state by viewModel.uiState.collectAsState()
    val actionError by viewModel.actionError.collectAsState()
    var menuExpanded by remember { mutableStateOf(false) }
    var showEditSheet by remember { mutableStateOf(false) }
    var showDeleteDialog by remember { mutableStateOf(false) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    val title = (state as? DeckDetailUiState.Loaded)?.detail?.name ?: "Deck"
                    Text(title)
                },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
                actions = {
                    val loaded = state as? DeckDetailUiState.Loaded
                    if (loaded != null) {
                        IconButton(onClick = { viewModel.toggleSuspended() }) {
                            if (loaded.detail.isSuspended) {
                                Icon(Icons.Default.PlayCircle, contentDescription = "Unsuspend deck")
                            } else {
                                Icon(Icons.Default.PauseCircle, contentDescription = "Suspend deck")
                            }
                        }
                        IconButton(onClick = { menuExpanded = true }) {
                            Icon(Icons.Default.MoreVert, contentDescription = "More")
                        }
                        DropdownMenu(expanded = menuExpanded, onDismissRequest = { menuExpanded = false }) {
                            DropdownMenuItem(
                                text = { Text("Edit deck") },
                                leadingIcon = { Icon(Icons.Default.Edit, null) },
                                onClick = {
                                    menuExpanded = false
                                    showEditSheet = true
                                },
                            )
                            DropdownMenuItem(
                                text = { Text("Copy deck ID") },
                                leadingIcon = { Icon(Icons.Outlined.ContentCopy, null) },
                                onClick = {
                                    menuExpanded = false
                                    copyToClipboard(context, "Deck ID", deckId)
                                    Toast.makeText(context, "Deck ID copied", Toast.LENGTH_SHORT).show()
                                },
                            )
                            DropdownMenuItem(
                                text = { Text("Delete deck") },
                                leadingIcon = { Icon(Icons.Default.Delete, null) },
                                onClick = {
                                    menuExpanded = false
                                    showDeleteDialog = true
                                },
                            )
                        }
                    }
                },
            )
        },
    ) { padding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding),
        ) {
            when (val s = state) {
                is DeckDetailUiState.Loading -> CircularProgressIndicator(Modifier.align(Alignment.Center))
                is DeckDetailUiState.Error -> Text(
                    s.message,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.padding(24.dp).align(Alignment.Center),
                )
                is DeckDetailUiState.Loaded -> DeckDetailBody(
                    detail = s.detail,
                    onCardClick = onCardClick,
                )
            }

            actionError?.let { msg ->
                Surface(
                    color = MaterialTheme.colorScheme.errorContainer,
                    modifier = Modifier
                        .align(Alignment.BottomCenter)
                        .fillMaxWidth()
                        .padding(16.dp),
                ) {
                    Row(
                        Modifier.padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        Text(
                            msg,
                            modifier = Modifier.weight(1f),
                            color = MaterialTheme.colorScheme.onErrorContainer,
                            style = MaterialTheme.typography.bodySmall,
                        )
                        TextButton(onClick = viewModel::clearActionError) { Text("Dismiss") }
                    }
                }
            }
        }

        if (showEditSheet) {
            val loaded = state as? DeckDetailUiState.Loaded
            DeckFormSheet(
                title = "Edit Deck",
                initialName = loaded?.detail?.name.orEmpty(),
                initialDescription = loaded?.detail?.description.orEmpty(),
                errorMessage = actionError,
                onSubmit = { name, description ->
                    viewModel.updateDeck(name, description) { success ->
                        if (success) showEditSheet = false
                    }
                },
                onDismiss = {
                    showEditSheet = false
                    viewModel.clearActionError()
                },
            )
        }

        if (showDeleteDialog) {
            var alsoDeleteCards by remember { mutableStateOf(false) }
            AlertDialog(
                onDismissRequest = { showDeleteDialog = false },
                title = { Text("Delete deck?") },
                text = {
                    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                        Text("This cannot be undone.")
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Checkbox(
                                checked = alsoDeleteCards,
                                onCheckedChange = { alsoDeleteCards = it },
                            )
                            Text("Also delete cards in this deck")
                        }
                    }
                },
                confirmButton = {
                    TextButton(onClick = {
                        viewModel.deleteDeck(alsoDeleteCards) { success ->
                            if (success) {
                                showDeleteDialog = false
                                onNavigateBack()
                            } else {
                                showDeleteDialog = false
                            }
                        }
                    }) { Text("Delete") }
                },
                dismissButton = {
                    TextButton(onClick = { showDeleteDialog = false }) { Text("Cancel") }
                },
            )
        }
    }
}

@Composable
private fun DeckDetailBody(
    detail: com.fasolt.android.data.api.models.DeckDetailDto,
    onCardClick: (String) -> Unit,
) {
    Column(Modifier.fillMaxSize()) {
        // Stats header — single thin ElevatedCard with two halves
        ElevatedCard(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 12.dp),
        ) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(vertical = 12.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                StatHalf(
                    value = detail.cardCount.toString(),
                    label = "Cards",
                    valueColor = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.weight(1f),
                )
                VerticalDivider(
                    modifier = Modifier.height(40.dp),
                )
                StatHalf(
                    value = detail.dueCount.toString(),
                    label = "Due",
                    valueColor = if (detail.dueCount > 0) {
                        MaterialTheme.colorScheme.primary
                    } else {
                        MaterialTheme.colorScheme.onSurfaceVariant
                    },
                    modifier = Modifier.weight(1f),
                )
            }
        }

        // Description as header subtitle (above the list)
        if (!detail.description.isNullOrBlank()) {
            Text(
                detail.description,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
            )
        }
        if (detail.isSuspended) {
            Text(
                "This deck is suspended. Cards are excluded from study.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
            )
        }

        if (detail.cards.isEmpty()) {
            Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Column(
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                    modifier = Modifier.padding(24.dp),
                ) {
                    Icon(
                        Icons.Outlined.Inbox,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(48.dp),
                    )
                    Text(
                        "No cards in this deck yet",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        } else {
            LazyColumn(
                modifier = Modifier.fillMaxSize(),
                contentPadding = PaddingValues(horizontal = 16.dp, vertical = 12.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                items(detail.cards, key = { it.id }) { card: DeckCardDto ->
                    DeckDetailCardRow(
                        card = card,
                        onClick = { onCardClick(card.id) },
                    )
                }
            }
        }
    }
}

@Composable
private fun StatHalf(
    value: String,
    label: String,
    valueColor: Color,
    modifier: Modifier = Modifier,
) {
    Column(
        modifier = modifier,
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(2.dp),
    ) {
        Text(
            value,
            style = MaterialTheme.typography.headlineSmall,
            color = valueColor,
        )
        Text(
            label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@OptIn(ExperimentalFoundationApi::class)
@Composable
private fun DeckDetailCardRow(
    card: DeckCardDto,
    onClick: () -> Unit,
) {
    val context = LocalContext.current
    var menuExpanded by remember { mutableStateOf(false) }

    ElevatedCard(
        modifier = Modifier
            .fillMaxWidth()
            .combinedClickable(
                onClick = onClick,
                onLongClick = { menuExpanded = true },
            ),
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = card.front,
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurface,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
                Spacer(Modifier.height(2.dp))
                Text(
                    text = card.back.lineSequence().firstOrNull().orEmpty(),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }
            CardStateBadge(state = card.state)

            DropdownMenu(
                expanded = menuExpanded,
                onDismissRequest = { menuExpanded = false },
            ) {
                DropdownMenuItem(
                    text = { Text("Copy ID") },
                    leadingIcon = {
                        Icon(Icons.Outlined.ContentCopy, contentDescription = null)
                    },
                    onClick = {
                        menuExpanded = false
                        copyToClipboard(context, "Card ID", card.id)
                        Toast.makeText(context, "Card ID copied", Toast.LENGTH_SHORT).show()
                    },
                )
            }
        }
    }
}

private fun copyToClipboard(context: Context, label: String, text: String) {
    val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
    clipboard.setPrimaryClip(ClipData.newPlainText(label, text))
}

@Composable
private fun CardStateBadge(state: String) {
    val (label, color) = when (state.lowercase()) {
        "new" -> "New" to MaterialTheme.colorScheme.secondary
        "learning" -> "Learning" to MaterialTheme.colorScheme.tertiary
        "review" -> "Review" to MaterialTheme.colorScheme.primary
        "relearn", "relearning" -> "Relearn" to MaterialTheme.colorScheme.error
        else -> state.replaceFirstChar { it.uppercase() } to MaterialTheme.colorScheme.secondary
    }
    Surface(
        shape = MaterialTheme.shapes.small,
        color = color.copy(alpha = 0.18f),
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall,
            color = color,
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
        )
    }
}
