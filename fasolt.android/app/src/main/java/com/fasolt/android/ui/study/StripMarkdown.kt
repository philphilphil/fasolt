package com.fasolt.android.ui.study

/**
 * Strips common markdown syntax for plain-text rendering. Mirrors
 * fasolt.ios/Fasolt/Utilities/String+StripMarkdown.swift so the two clients
 * render plain card text consistently.
 *
 * This is a best-effort regex pass — it's intentionally lossy and doesn't
 * try to parse markdown. For full rendering we'd reach for a Compose-aware
 * markdown library; here we just want clean text on the card face.
 */
internal fun String.stripMarkdown(): String {
    var result = this

    // Code block fences (``` or ```language) — multiline anchors.
    result = result.replace(Regex("(?m)^```\\w*\\n?"), "")
    result = result.replace(Regex("\\n?```$"), "")

    // Image syntax ![alt](url) → alt
    result = result.replace(Regex("!\\[([^\\]]*)\\]\\([^)]*\\)"), "$1")
    // Link syntax [text](url) → text
    result = result.replace(Regex("\\[([^\\]]*)\\]\\([^)]*\\)"), "$1")

    // Heading markers
    result = result.replace(Regex("(?m)^#{1,6}\\s+"), "")

    // Bold (** or __)
    result = result.replace(Regex("\\*\\*(.+?)\\*\\*"), "$1")
    result = result.replace(Regex("__(.+?)__"), "$1")

    // Italic (* or _) — the underscore variant avoids matching mid-word.
    result = result.replace(Regex("\\*(.+?)\\*"), "$1")
    result = result.replace(Regex("(?<!\\w)_(.+?)_(?!\\w)"), "$1")

    // Inline code
    result = result.replace(Regex("`([^`]+)`"), "$1")

    // Blockquote markers
    result = result.replace(Regex("(?m)^>\\s+"), "")

    // List markers
    result = result.replace(Regex("(?m)^[-*]\\s+"), "")
    result = result.replace(Regex("(?m)^\\d+\\.\\s+"), "")

    // Strikethrough
    result = result.replace(Regex("~~(.+?)~~"), "$1")

    // Collapse runs of spaces
    result = result.replace(Regex(" {2,}"), " ")

    return result.trim()
}
