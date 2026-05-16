package com.fasolt.android.ui.library

import android.app.Application
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewmodel.compose.viewModel
import com.fasolt.android.FasoltApplication
import com.fasolt.android.ui.cards.CardListViewModel
import com.fasolt.android.ui.cards.components.CardRow
import com.fasolt.android.ui.decks.DecksUiState
import com.fasolt.android.ui.decks.DecksViewModel
import com.fasolt.android.ui.decks.components.DeckFormSheet
import com.fasolt.android.ui.decks.components.DeckRow

enum class LibrarySegment(val label: String) {
    Decks("Decks"),
    Cards("Cards"),
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LibraryScreen(
    onDeckClick: (String) -> Unit,
    onCardClick: (String) -> Unit,
    onCreateCard: () -> Unit,
) {
    var selected by remember { mutableStateOf(LibrarySegment.Decks) }

    Scaffold(
        topBar = {
            TopAppBar(title = { Text("Library") })
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
                val entries = LibrarySegment.values()
                entries.forEachIndexed { index, segment ->
                    SegmentedButton(
                        selected = selected == segment,
                        onClick = { selected = segment },
                        shape = SegmentedButtonDefaults.itemShape(index = index, count = entries.size),
                    ) {
                        Text(segment.label)
                    }
                }
            }

            Box(modifier = Modifier.fillMaxSize()) {
                when (selected) {
                    LibrarySegment.Decks -> DecksBody(onDeckClick = onDeckClick)
                    LibrarySegment.Cards -> CardsBody(
                        onCardClick = onCardClick,
                        onCreateCard = onCreateCard,
                    )
                }
            }
        }
    }
}

@Composable
private fun DecksBody(
    onDeckClick: (String) -> Unit,
    viewModel: DecksViewModel = viewModel(),
) {
    val state by viewModel.uiState.collectAsState()
    val createError by viewModel.createError.collectAsState()
    var showCreateSheet by remember { mutableStateOf(false) }

    Box(modifier = Modifier.fillMaxSize()) {
        when (val s = state) {
            is DecksUiState.Loading -> CircularProgressIndicator(Modifier.align(Alignment.Center))
            is DecksUiState.Error -> Text(
                s.message,
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(24.dp).align(Alignment.Center),
            )
            is DecksUiState.Loaded -> {
                if (s.decks.isEmpty()) {
                    Text(
                        "No decks yet — tap + to create one",
                        modifier = Modifier.align(Alignment.Center),
                    )
                } else {
                    LazyColumn(
                        modifier = Modifier.fillMaxSize(),
                        contentPadding = PaddingValues(16.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        items(s.decks, key = { it.id }) { deck ->
                            DeckRow(deck = deck, onClick = { onDeckClick(deck.id) })
                        }
                    }
                }
            }
        }

        FloatingActionButton(
            onClick = { showCreateSheet = true },
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .padding(16.dp),
        ) {
            Icon(Icons.Default.Add, contentDescription = "New deck")
        }

        if (showCreateSheet) {
            DeckFormSheet(
                title = "New Deck",
                errorMessage = createError,
                onSubmit = { name, description ->
                    viewModel.createDeck(name, description) { success ->
                        if (success) showCreateSheet = false
                    }
                },
                onDismiss = {
                    showCreateSheet = false
                    viewModel.clearCreateError()
                },
            )
        }
    }
}

@Composable
private fun CardsBody(
    onCardClick: (String) -> Unit,
    onCreateCard: () -> Unit,
) {
    val context = LocalContext.current
    val app = context.applicationContext as FasoltApplication
    val viewModel: CardListViewModel = viewModel(
        key = "card-list-library",
        factory = object : ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : ViewModel> create(modelClass: Class<T>): T =
                CardListViewModel(app as Application, null, null) as T
        },
    )

    val cards by viewModel.cards.collectAsState()
    val isLoading by viewModel.isLoading.collectAsState()
    val isLoadingMore by viewModel.isLoadingMore.collectAsState()
    val error by viewModel.error.collectAsState()
    val listState = rememberLazyListState()

    LaunchedEffect(listState, cards) {
        snapshotFlow {
            val layoutInfo = listState.layoutInfo
            val lastVisible = layoutInfo.visibleItemsInfo.lastOrNull()?.index ?: -1
            val total = layoutInfo.totalItemsCount
            lastVisible to total
        }.collect { (lastVisible, total) ->
            if (total > 0 && lastVisible >= total - 5) {
                viewModel.loadMoreIfNeeded()
            }
        }
    }

    Box(modifier = Modifier.fillMaxSize()) {
        when {
            isLoading && cards.isEmpty() -> CircularProgressIndicator(Modifier.align(Alignment.Center))
            error != null && cards.isEmpty() -> Text(
                error ?: "",
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(24.dp).align(Alignment.Center),
            )
            cards.isEmpty() -> Text(
                "No cards yet — tap + to create one",
                modifier = Modifier.align(Alignment.Center),
            )
            else -> LazyColumn(
                state = listState,
                modifier = Modifier.fillMaxSize(),
                contentPadding = PaddingValues(16.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                items(cards, key = { it.id }) { card ->
                    CardRow(
                        front = card.front,
                        back = card.back,
                        state = card.state,
                        isSuspended = card.isSuspended,
                        sourceFile = card.sourceFile,
                        onClick = { onCardClick(card.id) },
                    )
                }
                if (isLoadingMore) {
                    item {
                        Box(Modifier.fillMaxSize().padding(16.dp), contentAlignment = Alignment.Center) {
                            CircularProgressIndicator()
                        }
                    }
                }
            }
        }

        FloatingActionButton(
            onClick = onCreateCard,
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .padding(16.dp),
        ) {
            Icon(Icons.Default.Add, contentDescription = "New card")
        }
    }
}
