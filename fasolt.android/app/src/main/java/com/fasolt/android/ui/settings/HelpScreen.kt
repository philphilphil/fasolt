package com.fasolt.android.ui.settings

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HelpScreen(onBack: () -> Unit) {
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("How It Works") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
    ) { padding ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding),
            contentPadding = PaddingValues(horizontal = 16.dp, vertical = 12.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            item {
                HelpSection {
                    Text(
                        "Fasolt uses FSRS (Free Spaced Repetition Scheduler) to schedule your reviews. Here's how it works.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }

            item {
                HelpSection(title = "What is Spaced Repetition?") {
                    Text(
                        "Spaced repetition is a study technique where you review material at increasing intervals. Instead of cramming, you see a card right before you're likely to forget it. Each successful recall makes the memory stronger, so the next review can wait longer.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
            }

            item {
                HelpSection(title = "The FSRS Algorithm") {
                    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                        Text(
                            "FSRS tracks three variables for each card:",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurface,
                        )
                        HelpItem(
                            title = "Stability (S)",
                            description = "How long the memory lasts. Higher stability means longer intervals between reviews.",
                        )
                        HelpItem(
                            title = "Difficulty (D)",
                            description = "How inherently hard the card is for you. Updated with each review based on your rating.",
                        )
                        HelpItem(
                            title = "Retrievability (R)",
                            description = "The probability you can recall the card right now. Decays over time — when it drops below the target retention, the card becomes due.",
                        )
                    }
                }
            }

            item {
                HelpSection(title = "How Reviews Work") {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text(
                            "When you review a card, you rate how well you recalled it:",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurface,
                        )
                        RatingItem(
                            rating = "Again",
                            color = MaterialTheme.colorScheme.error,
                            description = "You forgot. Stability resets and the card re-enters the learning phase.",
                        )
                        RatingItem(
                            rating = "Hard",
                            color = Color(0xFFE08A2A),
                            description = "You recalled with significant difficulty. Stability increases slightly, difficulty goes up.",
                        )
                        RatingItem(
                            rating = "Good",
                            color = Color(0xFF2E8B57),
                            description = "Normal recall. Stability increases proportionally — the standard path.",
                        )
                        RatingItem(
                            rating = "Easy",
                            color = MaterialTheme.colorScheme.primary,
                            description = "Effortless recall. Large stability increase, difficulty decreases.",
                        )
                    }
                }
            }

            item {
                HelpSection(title = "How Intervals Grow") {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text(
                            "A typical progression for a card rated \"Good\" each time:",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurface,
                        )
                        Row(
                            horizontalArrangement = Arrangement.spacedBy(6.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            listOf("1d", "3d", "8d", "21d", "55d", "4mo").forEachIndexed { idx, interval ->
                                Surface(
                                    shape = RoundedCornerShape(4.dp),
                                    color = MaterialTheme.colorScheme.surfaceVariant,
                                ) {
                                    Text(
                                        interval,
                                        style = MaterialTheme.typography.bodySmall,
                                        modifier = Modifier.padding(horizontal = 6.dp, vertical = 3.dp),
                                    )
                                }
                                if (idx < 5) {
                                    Text(
                                        "→",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    )
                                }
                            }
                        }
                        Text(
                            "Intervals grow roughly exponentially. \"Easy\" makes them grow faster; \"Again\" resets them.",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurface,
                        )
                    }
                }
            }

            item {
                HelpSection(title = "Card States") {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        HelpItem(
                            title = "New",
                            description = "Never reviewed. Will enter the learning phase on first review.",
                        )
                        HelpItem(
                            title = "Learning",
                            description = "Recently introduced or reset. Reviewed at short intervals until stable.",
                        )
                        HelpItem(
                            title = "Review",
                            description = "Graduated from learning. Intervals grow with each successful recall.",
                        )
                        HelpItem(
                            title = "Relearning",
                            description = "Previously known but forgotten (rated \"Again\"). Short intervals until re-stabilized.",
                        )
                        HelpItem(
                            title = "Suspended",
                            description = "Temporarily excluded from review. Can be unsuspended at any time.",
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun HelpSection(title: String? = null, content: @Composable () -> Unit) {
    ElevatedCard(modifier = Modifier.fillMaxWidth()) {
        Column(Modifier.padding(20.dp)) {
            if (title != null) {
                Text(
                    title,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                )
                Spacer(Modifier.height(12.dp))
            }
            content()
        }
    }
}

@Composable
private fun HelpItem(title: String, description: String) {
    Column {
        Text(
            title,
            style = MaterialTheme.typography.titleSmall,
            fontWeight = FontWeight.Medium,
        )
        Text(
            description,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

@Composable
private fun RatingItem(rating: String, color: Color, description: String) {
    Row(verticalAlignment = Alignment.Top) {
        Text(
            rating,
            style = MaterialTheme.typography.titleSmall,
            fontWeight = FontWeight.SemiBold,
            color = color,
            modifier = Modifier.width(56.dp),
        )
        Text(
            description,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}
