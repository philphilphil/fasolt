package com.fasolt.android.ui.study

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Pause
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun StudyScreen(
    deckId: String?,
    onExit: () -> Unit,
    viewModel: StudyViewModel = viewModel(),
) {
    val state by viewModel.uiState.collectAsState()
    val haptics = LocalHapticFeedback.current

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
                        Text(
                            text = "${s.currentIndex + 1} / ${s.totalCards}",
                            style = MaterialTheme.typography.titleMedium,
                            color = MaterialTheme.colorScheme.onSurface,
                        )
                    }
                },
                navigationIcon = {
                    if (state is StudyUiState.Studying) {
                        Row {
                            IconButton(onClick = {
                                haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                                viewModel.suspendCard()
                            }) {
                                Icon(
                                    Icons.Filled.Pause,
                                    contentDescription = "Suspend card",
                                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                            TextButton(onClick = {
                                haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                                viewModel.skipCard()
                            }) {
                                Text(
                                    text = "Skip",
                                    style = MaterialTheme.typography.bodyMedium,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                        }
                    }
                },
                actions = {
                    if (state !is StudyUiState.Summary) {
                        IconButton(onClick = {
                            if (state is StudyUiState.Studying && viewModel.hasProgress) {
                                viewModel.endSessionEarly()
                            } else {
                                onExit()
                            }
                        }) {
                            Icon(
                                Icons.Outlined.Close,
                                contentDescription = "Close",
                                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.surface,
                ),
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
                    onFlip = {
                        haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                        viewModel.flip()
                    },
                    onRate = { rating ->
                        haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                        viewModel.rate(rating)
                    },
                )
                is StudyUiState.Summary -> StudySummaryScreen(
                    cardsStudied = s.cardsStudied,
                    ratingsCount = s.ratingsCount,
                    failedRatings = s.failedRatings,
                    skippedCount = s.skippedCount,
                    suspendedCount = s.suspendedCount,
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
    val navBarPadding = WindowInsets.navigationBars.asPaddingValues().calculateBottomPadding()

    Column(modifier = Modifier.fillMaxSize()) {
        LinearProgressIndicator(
            progress = { state.progress },
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 8.dp)
                .height(4.dp),
            color = MaterialTheme.colorScheme.primary,
            trackColor = MaterialTheme.colorScheme.primary.copy(alpha = 0.18f),
        )

        Box(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .padding(horizontal = 16.dp),
            contentAlignment = Alignment.Center,
        ) {
            val card = state.currentCard ?: return@Box

            val targetRotation = if (state.isFlipped) 180f else 0f
            val rotation by animateFloatAsState(
                targetValue = targetRotation,
                animationSpec = tween(durationMillis = 400),
                label = "cardFlip",
            )
            val showFront = rotation <= 90f

            Box(
                modifier = Modifier
                    .fillMaxHeight(0.9f)
                    .fillMaxWidth()
                    .graphicsLayer {
                        rotationY = rotation
                        cameraDistance = 12f * density
                    }
                    .then(if (!state.isFlipped) Modifier.clickable { onFlip() } else Modifier),
            ) {
                if (showFront) {
                    CardDisplay(
                        card = card,
                        isFlipped = false,
                        modifier = Modifier.fillMaxWidth(),
                    )
                } else {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .graphicsLayer { rotationY = 180f },
                    ) {
                        CardDisplay(
                            card = card,
                            isFlipped = true,
                            modifier = Modifier.fillMaxWidth(),
                        )
                    }
                }
            }
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
                modifier = Modifier.padding(
                    start = 16.dp,
                    end = 16.dp,
                    bottom = 12.dp + navBarPadding,
                ),
            )
        } else {
            Button(
                onClick = onFlip,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(
                        start = 16.dp,
                        end = 16.dp,
                        bottom = 12.dp + navBarPadding,
                    )
                    .height(56.dp),
                shape = MaterialTheme.shapes.large,
            ) {
                Text(
                    text = "Show Answer",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                )
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
        RatingButton(
            label = "Again",
            rating = StudyRatings.AGAIN,
            containerColor = MaterialTheme.colorScheme.errorContainer,
            contentColor = MaterialTheme.colorScheme.onErrorContainer,
            isRating = isRating,
            onRate = onRate,
            modifier = Modifier.weight(1f),
        )
        RatingButton(
            label = "Hard",
            rating = StudyRatings.HARD,
            containerColor = MaterialTheme.colorScheme.tertiaryContainer,
            contentColor = MaterialTheme.colorScheme.onTertiaryContainer,
            isRating = isRating,
            onRate = onRate,
            modifier = Modifier.weight(1f),
        )
        RatingButton(
            label = "Good",
            rating = StudyRatings.GOOD,
            containerColor = MaterialTheme.colorScheme.primaryContainer,
            contentColor = MaterialTheme.colorScheme.onPrimaryContainer,
            isRating = isRating,
            onRate = onRate,
            modifier = Modifier.weight(1f),
        )
        RatingButton(
            label = "Easy",
            rating = StudyRatings.EASY,
            containerColor = MaterialTheme.colorScheme.secondaryContainer,
            contentColor = MaterialTheme.colorScheme.onSecondaryContainer,
            isRating = isRating,
            onRate = onRate,
            modifier = Modifier.weight(1f),
        )
    }
}

@Composable
private fun RatingButton(
    label: String,
    rating: String,
    containerColor: Color,
    contentColor: Color,
    isRating: Boolean,
    onRate: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    Button(
        onClick = { onRate(rating) },
        enabled = !isRating,
        modifier = modifier.height(56.dp),
        shape = MaterialTheme.shapes.medium,
        contentPadding = PaddingValues(vertical = 8.dp, horizontal = 4.dp),
        colors = ButtonDefaults.buttonColors(
            containerColor = containerColor,
            contentColor = contentColor,
        ),
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelLarge,
            fontWeight = FontWeight.SemiBold,
        )
    }
}
