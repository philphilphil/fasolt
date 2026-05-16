package com.fasolt.android.ui.cards.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp

/** Small colored pill that visualises the FSRS card state. Unknown states fall back to a neutral grey. */
@Composable
fun StateBadge(state: String, isSuspended: Boolean = false, modifier: Modifier = Modifier) {
    val (bg, fg, label) = when {
        isSuspended -> Triple(
            MaterialTheme.colorScheme.surfaceVariant,
            MaterialTheme.colorScheme.onSurfaceVariant,
            "suspended",
        )
        state.equals("new", ignoreCase = true) -> Triple(
            Color(0xFF3B82F6).copy(alpha = 0.15f),
            Color(0xFF1D4ED8),
            "new",
        )
        state.equals("learning", ignoreCase = true) -> Triple(
            Color(0xFFF59E0B).copy(alpha = 0.15f),
            Color(0xFFB45309),
            "learning",
        )
        state.equals("review", ignoreCase = true) -> Triple(
            Color(0xFF10B981).copy(alpha = 0.15f),
            Color(0xFF047857),
            "review",
        )
        state.equals("relearning", ignoreCase = true) -> Triple(
            Color(0xFFEF4444).copy(alpha = 0.15f),
            Color(0xFFB91C1C),
            "relearning",
        )
        else -> Triple(
            MaterialTheme.colorScheme.surfaceVariant,
            MaterialTheme.colorScheme.onSurfaceVariant,
            state.lowercase(),
        )
    }

    Text(
        text = label,
        style = MaterialTheme.typography.labelSmall,
        color = fg,
        modifier = modifier
            .background(bg, RoundedCornerShape(50))
            .padding(horizontal = 8.dp, vertical = 2.dp),
    )
}
