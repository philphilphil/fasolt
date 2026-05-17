package com.fasolt.android.ui.cards.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * Compact two-line card representation used in the cards list and in a deck's detail view.
 */
@Composable
fun CardRow(
    front: String,
    back: String,
    state: String,
    isSuspended: Boolean,
    sourceFile: String? = null,
    onClick: (() -> Unit)? = null,
    modifier: Modifier = Modifier,
) {
    @Suppress("UNUSED_PARAMETER") val _back = back
    val rowModifier = modifier.fillMaxWidth()
    if (onClick != null) {
        ElevatedCard(onClick = onClick, modifier = rowModifier) {
            CardRowContent(
                front = front,
                state = state,
                isSuspended = isSuspended,
                sourceFile = sourceFile,
            )
        }
    } else {
        ElevatedCard(modifier = rowModifier) {
            CardRowContent(
                front = front,
                state = state,
                isSuspended = isSuspended,
                sourceFile = sourceFile,
            )
        }
    }
}

@Composable
private fun CardRowContent(
    front: String,
    state: String,
    isSuspended: Boolean,
    sourceFile: String?,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(16.dp),
        horizontalArrangement = Arrangement.spacedBy(12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        StatePill(state = state)

        Column(
            modifier = Modifier.weight(1f),
            verticalArrangement = Arrangement.spacedBy(2.dp),
        ) {
            Text(
                text = front,
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
            )
            val metadata = buildMetadata(sourceFile = sourceFile, isSuspended = isSuspended)
            if (metadata != null) {
                Text(
                    text = metadata,
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }
        }
    }
}

private fun buildMetadata(sourceFile: String?, isSuspended: Boolean): String? {
    val parts = buildList {
        if (!sourceFile.isNullOrBlank()) add(sourceFile)
        if (isSuspended) add("suspended")
    }
    return if (parts.isEmpty()) null else parts.joinToString(" · ")
}

@Composable
private fun StatePill(state: String) {
    val color = stateColor(state)
    val initial = stateInitial(state)
    Surface(
        shape = CircleShape,
        color = color.copy(alpha = 0.15f),
        modifier = Modifier.size(36.dp),
    ) {
        Box(contentAlignment = Alignment.Center) {
            Text(
                text = initial,
                color = color,
                fontWeight = FontWeight.Bold,
                fontSize = 14.sp,
            )
        }
    }
}

@Composable
private fun stateColor(state: String): Color {
    return when (state.lowercase()) {
        "new" -> MaterialTheme.colorScheme.secondary
        "learning" -> MaterialTheme.colorScheme.tertiary
        "review" -> MaterialTheme.colorScheme.primary
        "relearn", "relearning" -> MaterialTheme.colorScheme.error
        else -> MaterialTheme.colorScheme.onSurfaceVariant
    }
}

private fun stateInitial(state: String): String {
    return when (state.lowercase()) {
        "new" -> "N"
        "learning" -> "L"
        "review" -> "R"
        "relearn", "relearning" -> "Re"
        else -> state.take(1).uppercase().ifEmpty { "?" }
    }
}
