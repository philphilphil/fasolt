package com.fasolt.android.ui.cards

import android.app.Application
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshots.SnapshotStateList
import androidx.compose.runtime.toMutableStateList
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewmodel.compose.viewModel
import com.fasolt.android.FasoltApplication

@OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)
@Composable
fun CardFormScreen(
    cardId: String?,
    onNavigateBack: () -> Unit,
    initialDeckId: String? = null,
) {
    val context = LocalContext.current
    val app = context.applicationContext as FasoltApplication
    val viewModel: CardFormViewModel = viewModel(
        key = "card-form-${cardId ?: "new"}",
        factory = object : ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : ViewModel> create(modelClass: Class<T>): T =
                CardFormViewModel(app as Application, cardId, initialDeckId) as T
        },
    )

    val loadState by viewModel.loadState.collectAsState()
    val card by viewModel.card.collectAsState()
    val decks by viewModel.decks.collectAsState()
    val isSaving by viewModel.isSaving.collectAsState()
    val error by viewModel.error.collectAsState()

    var front by remember { mutableStateOf("") }
    var back by remember { mutableStateOf("") }
    var sourceFile by remember { mutableStateOf("") }
    var sourceHeading by remember { mutableStateOf("") }
    val selectedDeckIds: SnapshotStateList<String> = remember { mutableListOf<String>().toMutableStateList() }
    var initialized by remember { mutableStateOf(false) }
    var showDeleteDialog by remember { mutableStateOf(false) }

    // Seed editable state once both the card (or null) and decks have loaded.
    LaunchedEffect(loadState, card) {
        if (!initialized && loadState is CardFormLoadState.Ready) {
            card?.let { c ->
                front = c.front
                back = c.back
                sourceFile = c.sourceFile.orEmpty()
                sourceHeading = c.sourceHeading.orEmpty()
                selectedDeckIds.clear()
                selectedDeckIds.addAll(c.decks.map { it.id })
            }
            if (card == null && initialDeckId != null) {
                selectedDeckIds.add(initialDeckId)
            }
            initialized = true
        }
    }

    val canSave = front.trim().isNotEmpty() && back.trim().isNotEmpty() && !isSaving

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(if (viewModel.isEdit) "Edit Card" else "New Card") },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
                actions = {
                    if (viewModel.isEdit) {
                        IconButton(onClick = { showDeleteDialog = true }) {
                            Icon(Icons.Default.Delete, contentDescription = "Delete card")
                        }
                    }
                    TextButton(
                        enabled = canSave,
                        onClick = {
                            viewModel.save(
                                front = front.trim(),
                                back = back.trim(),
                                sourceFile = sourceFile.trim().ifEmpty { null },
                                sourceHeading = sourceHeading.trim().ifEmpty { null },
                                deckIds = selectedDeckIds.toList(),
                            ) { success -> if (success) onNavigateBack() }
                        },
                    ) { Text("Save") }
                },
            )
        },
    ) { padding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding),
        ) {
            when (val ls = loadState) {
                is CardFormLoadState.Loading -> CircularProgressIndicator(Modifier.align(Alignment.Center))
                is CardFormLoadState.Error -> Text(
                    ls.message,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.padding(24.dp).align(Alignment.Center),
                )
                is CardFormLoadState.Ready -> Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .verticalScroll(rememberScrollState())
                        .padding(16.dp),
                    verticalArrangement = Arrangement.spacedBy(16.dp),
                ) {
                    OutlinedTextField(
                        value = front,
                        onValueChange = { front = it },
                        label = { Text("Front") },
                        modifier = Modifier.fillMaxWidth().heightIn(min = 96.dp),
                    )
                    OutlinedTextField(
                        value = back,
                        onValueChange = { back = it },
                        label = { Text("Back") },
                        modifier = Modifier.fillMaxWidth().heightIn(min = 96.dp),
                    )

                    Text("Source (optional)", style = MaterialTheme.typography.titleSmall)
                    OutlinedTextField(
                        value = sourceFile,
                        onValueChange = { sourceFile = it },
                        label = { Text("Source file") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                    )
                    OutlinedTextField(
                        value = sourceHeading,
                        onValueChange = { sourceHeading = it },
                        label = { Text("Heading") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                    )

                    if (decks.isNotEmpty()) {
                        Text("Decks", style = MaterialTheme.typography.titleSmall)
                        FlowRow(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                            verticalArrangement = Arrangement.spacedBy(8.dp),
                        ) {
                            decks.forEach { deck ->
                                val selected = selectedDeckIds.contains(deck.id)
                                FilterChip(
                                    selected = selected,
                                    onClick = {
                                        if (selected) selectedDeckIds.remove(deck.id)
                                        else selectedDeckIds.add(deck.id)
                                    },
                                    label = { Text(deck.name) },
                                )
                            }
                        }
                        if (!viewModel.isEdit && selectedDeckIds.size > 1) {
                            Text(
                                "Note: only the first selected deck is applied at creation. " +
                                    "Edit the card after to assign it to multiple decks.",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                    }

                    error?.let { msg ->
                        Text(msg, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
                    }
                }
            }
        }

        if (showDeleteDialog) {
            AlertDialog(
                onDismissRequest = { showDeleteDialog = false },
                title = { Text("Delete card?") },
                text = { Text("This cannot be undone.") },
                confirmButton = {
                    TextButton(onClick = {
                        viewModel.delete { success ->
                            showDeleteDialog = false
                            if (success) onNavigateBack()
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
