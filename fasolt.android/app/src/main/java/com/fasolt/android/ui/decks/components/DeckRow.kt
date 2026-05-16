package com.fasolt.android.ui.decks.components

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.fasolt.android.data.api.models.DeckDto

@Composable
fun DeckRow(
    deck: DeckDto,
    modifier: Modifier = Modifier,
    onClick: (() -> Unit)? = null,
) {
    val cardModifier = modifier.fillMaxWidth().let {
        if (onClick != null) it.then(Modifier) else it
    }
    if (onClick != null) {
        Card(
            modifier = cardModifier,
            onClick = onClick,
            elevation = CardDefaults.cardElevation(),
        ) {
            DeckRowContent(deck)
        }
    } else {
        Card(modifier = cardModifier) {
            DeckRowContent(deck)
        }
    }
}

@Composable
private fun DeckRowContent(deck: DeckDto) {
    Column(Modifier.padding(16.dp)) {
        Text(deck.name, style = MaterialTheme.typography.titleMedium)
        if (!deck.description.isNullOrBlank()) {
            Text(
                deck.description,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        val suffix = if (deck.isSuspended) " · suspended" else ""
        Text(
            text = "${deck.cardCount} cards · ${deck.dueCount} due$suffix",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}
