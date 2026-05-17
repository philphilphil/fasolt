package com.fasolt.android.ui.study

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.LocalContentColor
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp

/**
 * Renders a small subset of markdown using Compose primitives — covers the
 * common cases that show up on flashcards (headings, lists, inline bold/italic/
 * code, links).
 *
 * iOS's StudyView pipes card text through the Textual package's StructuredText;
 * this is the Compose-side equivalent. It's intentionally compact — for
 * full-fidelity rendering we'd reach for a markdown library, but cards are
 * short and the common syntax is bounded.
 */
@Composable
fun MarkdownText(
    text: String,
    modifier: Modifier = Modifier,
    style: TextStyle = MaterialTheme.typography.bodyLarge,
    color: Color = LocalContentColor.current,
) {
    val blocks = parseBlocks(text)
    Column(modifier = modifier) {
        blocks.forEachIndexed { index, block ->
            // A bit of space between blocks but no leading top padding.
            val topPad = if (index == 0) 0.dp else 8.dp
            when (block) {
                is MdBlock.Heading -> Text(
                    text = renderInline(block.text),
                    style = headingStyle(block.level),
                    color = color,
                    modifier = Modifier.padding(top = topPad),
                )
                is MdBlock.Paragraph -> Text(
                    text = renderInline(block.text),
                    style = style,
                    color = color,
                    modifier = Modifier.padding(top = topPad),
                )
                is MdBlock.Bullet -> Row(modifier = Modifier.padding(top = topPad)) {
                    Text("•  ", style = style, color = color)
                    Text(
                        text = renderInline(block.text),
                        style = style,
                        color = color,
                        modifier = Modifier.weight(1f),
                    )
                }
                is MdBlock.Numbered -> Row(modifier = Modifier.padding(top = topPad)) {
                    Text("${block.index}.  ", style = style, color = color)
                    Text(
                        text = renderInline(block.text),
                        style = style,
                        color = color,
                        modifier = Modifier.weight(1f),
                    )
                }
                is MdBlock.Blockquote -> Box(
                    modifier = Modifier
                        .padding(top = topPad)
                        .padding(start = 12.dp),
                ) {
                    Text(
                        text = renderInline(block.text),
                        style = style.copy(fontStyle = FontStyle.Italic),
                        color = color.copy(alpha = 0.75f),
                    )
                }
                is MdBlock.CodeBlock -> Text(
                    text = block.text,
                    style = style.copy(fontFamily = FontFamily.Monospace),
                    color = color,
                    modifier = Modifier.padding(top = topPad),
                )
            }
        }
    }
}

@Composable
private fun headingStyle(level: Int): TextStyle = when (level) {
    1 -> MaterialTheme.typography.headlineSmall
    2 -> MaterialTheme.typography.titleLarge
    else -> MaterialTheme.typography.titleMedium
}.copy(fontWeight = FontWeight.SemiBold)

private sealed class MdBlock {
    data class Heading(val level: Int, val text: String) : MdBlock()
    data class Paragraph(val text: String) : MdBlock()
    data class Bullet(val text: String) : MdBlock()
    data class Numbered(val index: Int, val text: String) : MdBlock()
    data class Blockquote(val text: String) : MdBlock()
    data class CodeBlock(val text: String) : MdBlock()
}

private fun parseBlocks(input: String): List<MdBlock> {
    val blocks = mutableListOf<MdBlock>()
    val lines = input.replace("\r\n", "\n").split("\n")
    var i = 0
    val paragraphBuf = StringBuilder()

    fun flushParagraph() {
        if (paragraphBuf.isNotEmpty()) {
            blocks += MdBlock.Paragraph(paragraphBuf.toString().trim())
            paragraphBuf.clear()
        }
    }

    while (i < lines.size) {
        val line = lines[i]
        val trimmed = line.trimEnd()

        // Fenced code block
        if (trimmed.startsWith("```")) {
            flushParagraph()
            val codeBuf = StringBuilder()
            i++
            while (i < lines.size && !lines[i].trimEnd().startsWith("```")) {
                if (codeBuf.isNotEmpty()) codeBuf.append('\n')
                codeBuf.append(lines[i])
                i++
            }
            blocks += MdBlock.CodeBlock(codeBuf.toString())
            if (i < lines.size) i++ // skip closing ```
            continue
        }

        if (trimmed.isBlank()) {
            flushParagraph()
            i++
            continue
        }

        val headingMatch = Regex("^(#{1,6})\\s+(.*)$").find(trimmed)
        if (headingMatch != null) {
            flushParagraph()
            val level = headingMatch.groupValues[1].length
            blocks += MdBlock.Heading(level, headingMatch.groupValues[2])
            i++
            continue
        }

        val bulletMatch = Regex("^[-*+]\\s+(.*)$").find(trimmed)
        if (bulletMatch != null) {
            flushParagraph()
            blocks += MdBlock.Bullet(bulletMatch.groupValues[1])
            i++
            continue
        }

        val numberedMatch = Regex("^(\\d+)\\.\\s+(.*)$").find(trimmed)
        if (numberedMatch != null) {
            flushParagraph()
            blocks += MdBlock.Numbered(
                numberedMatch.groupValues[1].toInt(),
                numberedMatch.groupValues[2],
            )
            i++
            continue
        }

        val quoteMatch = Regex("^>\\s?(.*)$").find(trimmed)
        if (quoteMatch != null) {
            flushParagraph()
            blocks += MdBlock.Blockquote(quoteMatch.groupValues[1])
            i++
            continue
        }

        if (paragraphBuf.isNotEmpty()) paragraphBuf.append(' ')
        paragraphBuf.append(trimmed)
        i++
    }
    flushParagraph()
    return blocks
}

/**
 * Inline-only markdown to AnnotatedString. Supports **bold**, *italic*,
 * _italic_, `code`, ~~strike~~, ![alt](url) → alt, [text](url) → text.
 */
internal fun renderInline(input: String): AnnotatedString {
    // First strip image/link syntax to plain labels — links aren't tappable in
    // our card view, but the label is still meaningful.
    val imageStripped = input.replace(Regex("!\\[([^\\]]*)\\]\\([^)]*\\)"), "$1")
    val linkStripped = imageStripped.replace(Regex("\\[([^\\]]*)\\]\\([^)]*\\)"), "$1")
    val src = linkStripped

    return buildAnnotatedString {
        var i = 0
        while (i < src.length) {
            // **bold**
            if (i + 1 < src.length && src[i] == '*' && src[i + 1] == '*') {
                val end = src.indexOf("**", startIndex = i + 2)
                if (end > 0) {
                    withStyle(SpanStyle(fontWeight = FontWeight.Bold)) {
                        appendInner(src.substring(i + 2, end))
                    }
                    i = end + 2
                    continue
                }
            }
            // __bold__
            if (i + 1 < src.length && src[i] == '_' && src[i + 1] == '_') {
                val end = src.indexOf("__", startIndex = i + 2)
                if (end > 0) {
                    withStyle(SpanStyle(fontWeight = FontWeight.Bold)) {
                        appendInner(src.substring(i + 2, end))
                    }
                    i = end + 2
                    continue
                }
            }
            // ~~strike~~
            if (i + 1 < src.length && src[i] == '~' && src[i + 1] == '~') {
                val end = src.indexOf("~~", startIndex = i + 2)
                if (end > 0) {
                    withStyle(SpanStyle(textDecoration = TextDecoration.LineThrough)) {
                        appendInner(src.substring(i + 2, end))
                    }
                    i = end + 2
                    continue
                }
            }
            // `inline code`
            if (src[i] == '`') {
                val end = src.indexOf('`', startIndex = i + 1)
                if (end > 0) {
                    withStyle(SpanStyle(fontFamily = FontFamily.Monospace)) {
                        append(src.substring(i + 1, end))
                    }
                    i = end + 1
                    continue
                }
            }
            // *italic*
            if (src[i] == '*') {
                val end = src.indexOf('*', startIndex = i + 1)
                if (end > 0 && end > i + 1) {
                    withStyle(SpanStyle(fontStyle = FontStyle.Italic)) {
                        appendInner(src.substring(i + 1, end))
                    }
                    i = end + 1
                    continue
                }
            }
            // _italic_ — only at word boundaries
            if (src[i] == '_' &&
                (i == 0 || !src[i - 1].isLetterOrDigit())
            ) {
                val end = src.indexOf('_', startIndex = i + 1)
                if (end > 0 && (end + 1 >= src.length || !src[end + 1].isLetterOrDigit())) {
                    withStyle(SpanStyle(fontStyle = FontStyle.Italic)) {
                        appendInner(src.substring(i + 1, end))
                    }
                    i = end + 1
                    continue
                }
            }
            append(src[i])
            i++
        }
    }
}

// Recursive-ish inline rendering for nested-style spans (e.g. bold + italic).
private fun androidx.compose.ui.text.AnnotatedString.Builder.appendInner(text: String) {
    append(renderInline(text))
}
