package com.fasolt.android.ui.study

import androidx.compose.foundation.background
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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

/**
 * End-of-session summary. Mirrors iOS StudySummaryView: shows total studied,
 * breakdown by rating, and Study-again / Done actions.
 */
@Composable
fun StudySummaryScreen(
    cardsStudied: Int,
    ratingsCount: Map<String, Int>,
    failedRatings: Int,
    onStudyAgain: () -> Unit,
    onDone: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            imageVector = Icons.Filled.CheckCircle,
            contentDescription = null,
            tint = Color(0xFF34C759),
            modifier = Modifier.size(64.dp),
        )

        Spacer(Modifier.height(16.dp))

        Text(
            text = if (cardsStudied == 0) "No cards due" else "Session Complete",
            style = MaterialTheme.typography.headlineSmall,
            fontWeight = FontWeight.Bold,
        )

        Spacer(Modifier.height(24.dp))

        Column(
            modifier = Modifier
                .fillMaxWidth()
                .background(
                    color = MaterialTheme.colorScheme.surfaceContainerHigh,
                    shape = RoundedCornerShape(12.dp),
                )
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            SummaryRow("Cards studied", cardsStudied.toString())
            HorizontalDivider()
            RatingRow("Again", ratingsCount[StudyRatings.AGAIN] ?: 0, Color(0xFFFF3B30))
            RatingRow("Hard", ratingsCount[StudyRatings.HARD] ?: 0, Color(0xFFFF9500))
            RatingRow("Good", ratingsCount[StudyRatings.GOOD] ?: 0, Color(0xFF34C759))
            RatingRow("Easy", ratingsCount[StudyRatings.EASY] ?: 0, Color(0xFF007AFF))
        }

        if (failedRatings > 0) {
            Spacer(Modifier.height(12.dp))
            Text(
                text = "$failedRatings rating${if (failedRatings == 1) "" else "s"} may not have been saved.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(horizontal = 8.dp),
            )
        }

        Spacer(Modifier.height(32.dp))

        if (cardsStudied > 0) {
            OutlinedButton(
                onClick = onStudyAgain,
                modifier = Modifier.fillMaxWidth(),
                contentPadding = PaddingValues(vertical = 12.dp),
            ) {
                Text("Study again")
            }
            Spacer(Modifier.height(12.dp))
        }

        Button(
            onClick = onDone,
            modifier = Modifier.fillMaxWidth(),
            colors = ButtonDefaults.buttonColors(),
            contentPadding = PaddingValues(vertical = 14.dp),
        ) {
            Text("Done")
        }
    }
}

@Composable
private fun SummaryRow(label: String, value: String) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.weight(1f),
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyLarge,
            fontWeight = FontWeight.SemiBold,
        )
    }
}

@Composable
private fun RatingRow(label: String, count: Int, tint: Color) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(10.dp)
                .background(tint, CircleShape),
        )
        Spacer(Modifier.size(8.dp))
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.weight(1f),
        )
        Text(
            text = count.toString(),
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.Medium,
        )
    }
}
