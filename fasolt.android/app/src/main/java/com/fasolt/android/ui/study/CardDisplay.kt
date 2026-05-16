package com.fasolt.android.ui.study

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.fasolt.android.data.api.models.DueCardDto

/**
 * Shared front/back display for a study card.
 *
 * If the side has an SVG, the SVG is rendered above the text; otherwise the
 * text is shown alone (markdown stripped to plain text — mirroring iOS).
 */
@Composable
fun CardDisplay(
    card: DueCardDto,
    isFlipped: Boolean,
    modifier: Modifier = Modifier,
) {
    val label = if (isFlipped) "Answer" else "Question"
    val text = if (isFlipped) card.back else card.front
    val svg = if (isFlipped) card.backSvg else card.frontSvg
    val sourceHeading = if (isFlipped) card.sourceHeading else null
    // When the answer is showing, repeat the question above as a small hint —
    // matches iOS's CardView questionText behaviour.
    val questionHint = if (isFlipped) card.front else null

    ElevatedCard(
        modifier = modifier.fillMaxWidth(),
        colors = CardDefaults.elevatedCardColors(
            containerColor = MaterialTheme.colorScheme.surfaceContainerHigh,
        ),
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(PaddingValues(horizontal = 16.dp, vertical = 14.dp)),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Text(
                text = label.uppercase(),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                fontWeight = FontWeight.SemiBold,
            )

            Spacer(Modifier.height(8.dp))

            if (!questionHint.isNullOrBlank()) {
                Text(
                    text = questionHint.stripMarkdown(),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 8.dp),
                )
                Spacer(Modifier.height(8.dp))
            }

            if (!svg.isNullOrBlank()) {
                SvgRenderer(
                    svg = svg,
                    modifier = Modifier.padding(bottom = 8.dp),
                )
            }

            // The text body is scrollable so long answers don't blow out the layout.
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .verticalScroll(rememberScrollState()),
            ) {
                Text(
                    text = text.stripMarkdown(),
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.padding(horizontal = 8.dp),
                )
            }

            if (!card.sourceFile.isNullOrBlank() || !sourceHeading.isNullOrBlank()) {
                Spacer(Modifier.height(8.dp))
                val source = buildString {
                    card.sourceFile?.let { append(it) }
                    if (!sourceHeading.isNullOrBlank()) {
                        if (isNotEmpty()) append(" · ")
                        append(sourceHeading)
                    }
                }
                Text(
                    text = source,
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 8.dp),
                )
            }
        }

        // Padding row below; keeps the card from feeling cramped at the bottom edge.
        Spacer(
            modifier = Modifier
                .fillMaxWidth()
                .height(4.dp)
                .padding(bottom = 4.dp),
        )
    }
}

@Composable
internal fun cardArrangementSpacing() = Arrangement.spacedBy(12.dp)
