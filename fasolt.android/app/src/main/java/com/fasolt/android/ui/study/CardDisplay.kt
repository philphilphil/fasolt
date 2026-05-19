package com.fasolt.android.ui.study

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
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
    val text = if (isFlipped) card.back else card.front
    val svg = if (isFlipped) card.backSvg else card.frontSvg
    // When the answer is showing, repeat the question above as a small hint —
    // matches iOS's CardView questionText behaviour.
    val questionHint = if (isFlipped) card.front else null

    ElevatedCard(
        modifier = modifier.fillMaxWidth(),
        shape = MaterialTheme.shapes.large,
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(24.dp),
        ) {
            if (!questionHint.isNullOrBlank()) {
                MarkdownText(
                    text = questionHint,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.fillMaxWidth(),
                )
                Spacer(Modifier.height(12.dp))
            }

            if (!svg.isNullOrBlank()) {
                SvgRenderer(
                    svg = svg,
                    modifier = Modifier.padding(bottom = 12.dp),
                )
            }

            // The text body is scrollable so long answers don't blow out the layout.
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .verticalScroll(rememberScrollState()),
            ) {
                MarkdownText(
                    text = text,
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.fillMaxWidth(),
                )
            }

            val source = card.sourceFile
            if (!source.isNullOrBlank()) {
                Spacer(Modifier.height(16.dp))
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Icon(
                        imageVector = Icons.Outlined.Description,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(14.dp),
                    )
                    Spacer(Modifier.size(6.dp))
                    Text(
                        text = source,
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }
    }
}

@Composable
internal fun cardArrangementSpacing() = Arrangement.spacedBy(12.dp)
