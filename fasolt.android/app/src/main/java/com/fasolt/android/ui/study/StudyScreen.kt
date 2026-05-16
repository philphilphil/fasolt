package com.fasolt.android.ui.study

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel

/**
 * Study (spaced-repetition review) screen — mirrors fasolt.ios StudyView.
 *
 * @param deckId optional deck to filter due cards by; null studies across all decks.
 * @param onExit invoked when the user dismisses the session or completes it.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun StudyScreen(
    deckId: String?,
    onExit: () -> Unit,
    viewModel: StudyViewModel = viewModel(),
) {
    val state by viewModel.uiState.collectAsState()

    // Kick off the session once on first composition. We key on deckId so a
    // re-navigation to a different deck restarts the session.
    LaunchedEffect(deckId) {
        if (state is StudyUiState.Idle) {
            viewModel.startSession(deckId)
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    if (state is StudyUiState.Studying) {
                        val s = state as StudyUiState.Studying
                        Text("${s.currentIndex + 1} / ${s.totalCards}")
                    } else {
                        Text("Study")
                    }
                },
                actions = {
                    if (state !is StudyUiState.Summary) {
                        IconButton(onClick = onExit) {
                            Icon(Icons.Default.Close, contentDescription = "Close")
                        }
                    }
                },
            )
        },
    ) { padding ->
        Box(modifier = Modifier
            .fillMaxSize()
            .padding(padding)) {
            when (val s = state) {
                is StudyUiState.Idle, is StudyUiState.Loading -> {
                    CircularProgressIndicator(Modifier.align(Alignment.Center))
                }
                is StudyUiState.Error -> {
                    Column(
                        modifier = Modifier
                            .align(Alignment.Center)
                            .padding(24.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                    ) {
                        Text(
                            text = s.message,
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.error,
                        )
                        Spacer(Modifier.height(16.dp))
                        Button(onClick = { viewModel.startSession(deckId) }) {
                            Text("Retry")
                        }
                    }
                }
                is StudyUiState.Studying -> StudyingContent(
                    state = s,
                    onFlip = viewModel::flip,
                    onRate = viewModel::rate,
                )
                is StudyUiState.Summary -> StudySummaryScreen(
                    cardsStudied = s.cardsStudied,
                    ratingsCount = s.ratingsCount,
                    failedRatings = s.failedRatings,
                    onStudyAgain = { viewModel.studyAgain(deckId) },
                    onDone = onExit,
                )
            }
        }
    }
}

@Composable
private fun StudyingContent(
    state: StudyUiState.Studying,
    onFlip: () -> Unit,
    onRate: (String) -> Unit,
) {
    Column(modifier = Modifier.fillMaxSize()) {
        // Progress bar
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            LinearProgressIndicator(
                progress = { state.progress },
                modifier = Modifier
                    .weight(1f)
                    .height(4.dp),
            )
        }

        // Card body — tap-to-flip when face down. We attach the click to a Box
        // wrapper so the entire card area is the hit target.
        Box(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .padding(horizontal = 16.dp),
            contentAlignment = Alignment.Center,
        ) {
            val card = state.currentCard ?: return@Box
            val flipModifier = if (!state.isFlipped) Modifier.clickable { onFlip() } else Modifier
            CardDisplay(
                card = card,
                isFlipped = state.isFlipped,
                modifier = flipModifier.fillMaxHeight(0.9f),
            )
        }

        if (state.ratingError != null) {
            Text(
                text = state.ratingError,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
            )
        }

        if (state.isFlipped) {
            RatingButtons(
                isRating = state.isRating,
                onRate = onRate,
                modifier = Modifier.padding(16.dp),
            )
        } else {
            Button(
                onClick = onFlip,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp),
                contentPadding = PaddingValues(vertical = 14.dp),
            ) {
                Text("Show Answer")
            }
        }
    }
}

@Composable
private fun RatingButtons(
    isRating: Boolean,
    onRate: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    Row(
        modifier = modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        RatingButton("Again", StudyRatings.AGAIN, Color(0xFFFF3B30), isRating, onRate, Modifier.weight(1f))
        RatingButton("Hard", StudyRatings.HARD, Color(0xFFFF9500), isRating, onRate, Modifier.weight(1f))
        RatingButton("Good", StudyRatings.GOOD, Color(0xFF34C759), isRating, onRate, Modifier.weight(1f))
        RatingButton("Easy", StudyRatings.EASY, Color(0xFF007AFF), isRating, onRate, Modifier.weight(1f))
    }
}

@Composable
private fun RatingButton(
    label: String,
    rating: String,
    tint: Color,
    isRating: Boolean,
    onRate: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    OutlinedButton(
        onClick = { onRate(rating) },
        enabled = !isRating,
        modifier = modifier,
        contentPadding = PaddingValues(vertical = 12.dp),
        colors = ButtonDefaults.outlinedButtonColors(contentColor = tint),
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelLarge,
            fontWeight = FontWeight.Medium,
        )
    }
}
