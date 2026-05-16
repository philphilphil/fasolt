package com.fasolt.android.ui.cards

import android.app.Application
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewmodel.compose.viewModel
import com.fasolt.android.FasoltApplication
import com.fasolt.android.ui.cards.components.CardRow

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CardListScreen(
    onCardClick: (String) -> Unit,
    onCreateCard: () -> Unit,
    onNavigateBack: (() -> Unit)? = null,
    deckIdFilter: String? = null,
    sourceFileFilter: String? = null,
) {
    val context = LocalContext.current
    val app = context.applicationContext as FasoltApplication
    val vmKey = "card-list-${deckIdFilter ?: "any"}-${sourceFileFilter ?: "any"}"
    val viewModel: CardListViewModel = viewModel(
        key = vmKey,
        factory = object : ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : ViewModel> create(modelClass: Class<T>): T =
                CardListViewModel(app as Application, deckIdFilter, sourceFileFilter) as T
        },
    )

    val cards by viewModel.cards.collectAsState()
    val isLoading by viewModel.isLoading.collectAsState()
    val isLoadingMore by viewModel.isLoadingMore.collectAsState()
    val error by viewModel.error.collectAsState()
    val listState = rememberLazyListState()

    // Trigger pagination when the user scrolls within ~5 items of the end.
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

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(if (deckIdFilter != null || sourceFileFilter != null) "Cards (filtered)" else "Cards") },
                navigationIcon = {
                    if (onNavigateBack != null) {
                        IconButton(onClick = onNavigateBack) {
                            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                        }
                    }
                },
            )
        },
        floatingActionButton = {
            FloatingActionButton(onClick = onCreateCard) {
                Icon(Icons.Default.Add, contentDescription = "New card")
            }
        },
    ) { padding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding),
        ) {
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
        }
    }
}
