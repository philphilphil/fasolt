package com.fasolt.android.ui.progress

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.LocalFireDepartment
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
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
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import com.fasolt.android.data.api.models.DailyActivity
import com.fasolt.android.data.api.models.ProgressDto
import java.time.DayOfWeek
import java.time.LocalDate
import java.time.format.DateTimeFormatter

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ProgressScreen(
    viewModel: ProgressViewModel = viewModel(),
) {
    val state by viewModel.uiState.collectAsState()

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Progress") },
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
                is ProgressUiState.Loading ->
                    CircularProgressIndicator(Modifier.align(Alignment.Center))

                is ProgressUiState.Error -> Column(
                    modifier = Modifier
                        .align(Alignment.Center)
                        .padding(24.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    Text(
                        text = s.message,
                        color = MaterialTheme.colorScheme.error,
                        textAlign = TextAlign.Center,
                    )
                    Button(onClick = viewModel::refresh) { Text("Retry") }
                }

                is ProgressUiState.Loaded -> ProgressContent(s.progress)
            }
        }
    }
}

@Composable
private fun ProgressContent(progress: ProgressDto) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        StreakCardsGrid(progress)
        PeriodStatsRow(progress)
        ActivityHeatmap(progress)
        if (progress.totalAnswered == 0) {
            Text(
                text = "No reviews yet. Once you start studying, your activity shows up here.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 4.dp),
            )
        }
    }
}

// MARK: - Stat cards

@Composable
private fun StreakCardsGrid(progress: ProgressDto) {
    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            StatCell(
                label = "CURRENT STREAK",
                value = progress.currentStreak.toString(),
                modifier = Modifier.weight(1f),
                leading = if (progress.currentStreak > 0) {
                    {
                        Icon(
                            imageVector = Icons.Default.LocalFireDepartment,
                            contentDescription = null,
                            tint = Color(0xFFFF9500),
                            modifier = Modifier.size(18.dp),
                        )
                        Spacer(Modifier.width(4.dp))
                    }
                } else null,
            )
            StatCell(
                label = "BEST STREAK",
                value = progress.bestStreak.toString(),
                modifier = Modifier.weight(1f),
            )
        }
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            StatCell(
                label = "TOTAL ANSWERED",
                value = progress.totalAnswered.toString(),
                modifier = Modifier.weight(1f),
            )
            StatCell(
                label = "TODAY",
                value = progress.answeredToday.toString(),
                modifier = Modifier.weight(1f),
            )
        }
    }
}

@Composable
private fun StatCell(
    label: String,
    value: String,
    modifier: Modifier = Modifier,
    leading: (@Composable () -> Unit)? = null,
) {
    Card(
        modifier = modifier,
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceVariant,
        ),
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(4.dp),
        ) {
            Text(
                text = label,
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Row(verticalAlignment = Alignment.CenterVertically) {
                if (leading != null) leading()
                Text(
                    text = value,
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.Bold,
                )
            }
        }
    }
}

@Composable
private fun PeriodStatsRow(progress: ProgressDto) {
    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
        StatCell(
            label = "THIS WEEK",
            value = progress.answeredThisWeek.toString(),
            modifier = Modifier.weight(1f),
        )
        StatCell(
            label = "THIS MONTH",
            value = progress.answeredThisMonth.toString(),
            modifier = Modifier.weight(1f),
        )
    }
}

// MARK: - Heatmap

private val RestColor = Color(0xFF222222).copy(alpha = 0.14f)
private val MissedColor = Color(0xFFFF9500).copy(alpha = 0.22f)
private val StudiedBase = Color(0xFF34C759)
private val DateParser: DateTimeFormatter = DateTimeFormatter.ofPattern("yyyy-MM-dd")

private data class HeatCell(
    val key: String,
    val day: DailyActivity?,
    val isToday: Boolean,
)

@Composable
private fun ActivityHeatmap(progress: ProgressDto) {
    val activity = progress.dailyActivity
    val maxCount = (activity.maxOfOrNull { it.count } ?: 0).coerceAtLeast(1)
    val studiedDays = activity.count { it.count > 0 }
    val cells = remember(activity) { buildCells(activity) }

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceVariant,
        ),
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    text = "LAST ${activity.size} DAYS",
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Spacer(Modifier.weight(1f))
                Text(
                    text = "$studiedDays of ${activity.size} studied",
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }

            // 7 columns, Monday-first. Use LazyVerticalGrid in non-scrollable mode
            // by giving it an explicit height — or simpler: chunk into weeks and lay
            // them out with plain Rows so we don't nest scrollables.
            HeatGrid(cells = cells, maxCount = maxCount)

            LegendRow()
        }
    }
}

@Composable
private fun HeatGrid(cells: List<HeatCell>, maxCount: Int) {
    val weeks = cells.chunked(7)
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        weeks.forEach { week ->
            Row(horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                week.forEach { cell ->
                    HeatBox(cell = cell, maxCount = maxCount)
                }
                // Pad row to 7 cells if final week is short.
                repeat(7 - week.size) {
                    Box(modifier = Modifier.size(26.dp))
                }
            }
        }
    }
}

@Composable
private fun HeatBox(cell: HeatCell, maxCount: Int) {
    val day = cell.day
    val accent = MaterialTheme.colorScheme.primary
    val fill = when {
        day == null -> Color.Transparent
        day.count > 0 -> {
            val intensity = day.count.toFloat() / maxCount.toFloat()
            val alpha = when {
                intensity < 0.25f -> 0.35f
                intensity < 0.5f -> 0.55f
                intensity < 0.75f -> 0.75f
                else -> 1.0f
            }
            StudiedBase.copy(alpha = alpha)
        }
        day.hadDue -> MissedColor
        else -> RestColor
    }

    var boxModifier: Modifier = Modifier
        .size(26.dp)
        .clip(RoundedCornerShape(5.dp))
        .background(fill)
    if (cell.isToday && day != null) {
        boxModifier = boxModifier.border(
            width = 1.5.dp,
            color = accent,
            shape = RoundedCornerShape(5.dp),
        )
    }

    Box(modifier = boxModifier, contentAlignment = Alignment.Center) {
        if (day != null && day.count > 0) {
            val intensity = day.count.toFloat() / maxCount.toFloat()
            val textColor = if (intensity >= 0.5f) Color.White else Color(0xFF1A4D26)
            Text(
                text = day.count.toString(),
                style = MaterialTheme.typography.labelSmall,
                color = textColor,
                fontWeight = FontWeight.SemiBold,
            )
        }
    }
}

@Composable
private fun LegendRow() {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        LegendSwatch(color = StudiedBase.copy(alpha = 0.7f), label = "Studied")
        LegendSwatch(color = MissedColor, label = "Missed")
        LegendSwatch(color = RestColor, label = "Rest")
        LegendOutline(color = MaterialTheme.colorScheme.primary, label = "Today")
    }
}

@Composable
private fun LegendSwatch(color: Color, label: String) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Box(
            modifier = Modifier
                .size(10.dp)
                .clip(RoundedCornerShape(3.dp))
                .background(color),
        )
        Spacer(Modifier.width(4.dp))
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun LegendOutline(color: Color, label: String) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Box(
            modifier = Modifier
                .size(10.dp)
                .border(1.5.dp, color, RoundedCornerShape(3.dp)),
        )
        Spacer(Modifier.width(4.dp))
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

private fun buildCells(activity: List<DailyActivity>): List<HeatCell> {
    if (activity.isEmpty()) return emptyList()

    val firstDate = runCatching { LocalDate.parse(activity.first().date, DateParser) }.getOrNull()
    val leadingEmpty = if (firstDate != null) {
        // Monday-first layout: Monday=0..Sunday=6
        ((firstDate.dayOfWeek.value - DayOfWeek.MONDAY.value) + 7) % 7
    } else {
        0
    }

    val cells = mutableListOf<HeatCell>()
    repeat(leadingEmpty) { i ->
        cells += HeatCell(key = "pad-$i", day = null, isToday = false)
    }
    val lastIdx = activity.size - 1
    activity.forEachIndexed { idx, day ->
        cells += HeatCell(key = day.date, day = day, isToday = idx == lastIdx)
    }
    return cells
}
