package com.fasolt.android.ui.dashboard

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.LocalFireDepartment
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import com.fasolt.android.data.api.models.Overview
import com.fasolt.android.data.api.models.StudyStats

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DashboardScreen(
    onStartStudy: () -> Unit,
    onOpenProgress: () -> Unit = {},
    viewModel: DashboardViewModel = viewModel(),
) {
    val state by viewModel.uiState.collectAsState()

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Dashboard") },
                actions = {
                    IconButton(onClick = viewModel::refresh) {
                        Icon(Icons.Default.Refresh, contentDescription = "Refresh")
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
                is DashboardUiState.Loading ->
                    CircularProgressIndicator(Modifier.align(Alignment.Center))

                is DashboardUiState.Error ->
                    ErrorBlock(
                        message = s.message,
                        onRetry = viewModel::refresh,
                        modifier = Modifier.align(Alignment.Center),
                    )

                is DashboardUiState.Loaded -> DashboardContent(
                    data = s.data,
                    onStartStudy = onStartStudy,
                    onOpenProgress = onOpenProgress,
                )
            }
        }
    }
}

@Composable
private fun ErrorBlock(message: String, onRetry: () -> Unit, modifier: Modifier = Modifier) {
    Column(
        modifier = modifier.padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text(
            message,
            color = MaterialTheme.colorScheme.error,
            textAlign = TextAlign.Center,
        )
        Button(onClick = onRetry) { Text("Retry") }
    }
}

@Composable
private fun DashboardContent(
    data: DashboardData,
    onStartStudy: () -> Unit,
    onOpenProgress: () -> Unit,
) {
    val overview = data.overview
    val stats = data.studyStats

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        if (stats.totalAnswered > 0) {
            StatsRow(stats = stats, onClick = onOpenProgress)
        }

        DueHeroCard(dueCount = overview.dueCards, onStartStudy = onStartStudy)

        StatTilesRow(overview = overview)

        if (overview.totalCards > 0) {
            CardsByStateCard(byState = overview.cardsByState, total = overview.totalCards)
        }
    }
}

// MARK: - Hero card

@Composable
private fun DueHeroCard(dueCount: Int, onStartStudy: () -> Unit) {
    val gradient = Brush.linearGradient(
        colors = listOf(
            MaterialTheme.colorScheme.primary,
            MaterialTheme.colorScheme.tertiary,
        ),
    )

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(20.dp))
            .background(gradient)
            .padding(vertical = 24.dp, horizontal = 20.dp),
        contentAlignment = Alignment.Center,
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            Text(
                text = "Cards due",
                color = Color.White.copy(alpha = 0.70f),
                style = MaterialTheme.typography.titleMedium,
            )
            Text(
                text = dueCount.toString(),
                color = Color.White,
                style = MaterialTheme.typography.displayLarge,
                fontWeight = FontWeight.Bold,
            )
            if (dueCount > 0) {
                Button(
                    onClick = onStartStudy,
                    shape = RoundedCornerShape(50),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = MaterialTheme.colorScheme.onPrimary,
                        contentColor = MaterialTheme.colorScheme.primary,
                    ),
                ) { Text("Start studying") }
            } else {
                Text(
                    text = "All caught up!",
                    color = Color.White.copy(alpha = 0.85f),
                    style = MaterialTheme.typography.bodyMedium,
                )
            }
        }
    }
}

// MARK: - Small stat tiles

@Composable
private fun StatTilesRow(overview: Overview) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        StatTile(label = "Total cards", value = overview.totalCards, modifier = Modifier.weight(1f))
        StatTile(label = "Decks", value = overview.totalDecks, modifier = Modifier.weight(1f))
        StatTile(label = "Sources", value = overview.totalSources, modifier = Modifier.weight(1f))
    }
}

@Composable
private fun StatTile(label: String, value: Int, modifier: Modifier = Modifier) {
    ElevatedCard(modifier = modifier) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(4.dp),
        ) {
            Text(
                text = label,
                style = MaterialTheme.typography.titleSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Text(
                text = value.toString(),
                style = MaterialTheme.typography.headlineMedium,
                fontWeight = FontWeight.Bold,
            )
        }
    }
}

// MARK: - Cards by state

private data class StateBucket(val key: String, val label: String, val color: Color)

@Composable
private fun CardsByStateCard(byState: Map<String, Int>, total: Int) {
    val buckets = listOf(
        StateBucket("new", "New", Color(0xFF34C759)),
        StateBucket("review", "Review", MaterialTheme.colorScheme.primary),
        StateBucket("learning", "Learning", Color(0xFFFF9500)),
        StateBucket("relearning", "Relearn", MaterialTheme.colorScheme.error),
    )

    ElevatedCard(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            Text(
                text = "By state",
                style = MaterialTheme.typography.titleSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )

            // Proportional bar
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(8.dp)
                    .clip(RoundedCornerShape(4.dp))
                    .background(MaterialTheme.colorScheme.surface),
            ) {
                buckets.forEach { bucket ->
                    val count = byState[bucket.key] ?: 0
                    if (count > 0 && total > 0) {
                        val weight = count.toFloat() / total.toFloat()
                        Box(
                            modifier = Modifier
                                .weight(weight)
                                .fillMaxSize()
                                .background(bucket.color),
                        )
                    }
                }
            }

            // Legend chips
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                buckets.forEach { bucket ->
                    StateLegend(
                        label = bucket.label,
                        count = byState[bucket.key] ?: 0,
                        color = bucket.color,
                    )
                }
            }
        }
    }
}

@Composable
private fun StateLegend(label: String, count: Int, color: Color) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Box(
            modifier = Modifier
                .size(8.dp)
                .clip(CircleShape)
                .background(color),
        )
        Spacer(Modifier.width(6.dp))
        Text(
            text = "$label $count",
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

// MARK: - Motivational stats block

@Composable
private fun StatsRow(stats: StudyStats, onClick: () -> Unit) {
    ElevatedCard(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 14.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(14.dp),
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = Icons.Default.LocalFireDepartment,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.tertiary,
                )
                Spacer(Modifier.width(4.dp))
                Text(
                    text = stats.currentStreak.toString(),
                    fontWeight = FontWeight.Bold,
                    style = MaterialTheme.typography.labelLarge,
                )
                Spacer(Modifier.width(4.dp))
                Text(
                    text = "day streak",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Spacer(Modifier.weight(1f))
            InlineStat(stats.answeredToday, "today")
            InlineStat(stats.totalAnswered, "total")
            InlineStat(stats.bestStreak, "best")
        }
    }
}

@Composable
private fun InlineStat(value: Int, label: String) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Text(
            text = value.toString(),
            fontWeight = FontWeight.SemiBold,
            style = MaterialTheme.typography.bodyMedium,
        )
        Spacer(Modifier.width(3.dp))
        Text(
            text = label,
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}
