package com.fasolt.android.ui.study

import android.annotation.SuppressLint
import android.graphics.Color
import android.util.Base64
import android.view.ViewGroup
import android.webkit.WebView
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import java.nio.charset.StandardCharsets

/**
 * Renders a raw SVG string inside a WebView. We pick WebView because:
 * - It's built into the platform (no new Gradle dep).
 * - It handles full SVG 1.1 / 2 features that a hand-rolled Compose renderer
 *   (or AndroidSVG, which is unmaintained) would miss.
 * - Coil's SvgDecoder would also work but pulls in a new dependency just for
 *   a single use site.
 *
 * The SVG is wrapped in a minimal HTML document that scales it to fit the
 * container width and uses transparent background, so it blends with the card.
 * If the SVG is malformed the WebView simply renders nothing — same graceful
 * degradation as a broken `<img>`.
 */
@Composable
fun SvgRenderer(
    svg: String,
    modifier: Modifier = Modifier,
    height: Dp = 240.dp,
) {
    val html = remember(svg) { wrapSvgInHtml(svg) }

    AndroidView(
        modifier = modifier.fillMaxWidth().height(height),
        factory = { context ->
            @SuppressLint("SetJavaScriptEnabled")
            WebView(context).apply {
                layoutParams = ViewGroup.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT,
                    ViewGroup.LayoutParams.MATCH_PARENT,
                )
                setBackgroundColor(Color.TRANSPARENT)
                isVerticalScrollBarEnabled = false
                isHorizontalScrollBarEnabled = false
                settings.javaScriptEnabled = false
                settings.allowFileAccess = false
                settings.allowContentAccess = false
                // Prevents pinch-zoom / scroll capture inside our scrollable parents.
                settings.setSupportZoom(false)
                settings.builtInZoomControls = false
                isClickable = false
                isFocusable = false
                isFocusableInTouchMode = false
            }
        },
        update = { web ->
            // base64-encoded data URL is the most robust path — avoids quoting/escaping
            // pitfalls of inline `data:image/svg+xml,...` URLs.
            val bytes = html.toByteArray(StandardCharsets.UTF_8)
            val encoded = Base64.encodeToString(bytes, Base64.NO_PADDING or Base64.NO_WRAP)
            web.loadData(encoded, "text/html; charset=utf-8", "base64")
        },
    )
}

// Wraps a raw <svg>...</svg> string in a centred, transparent HTML page that
// scales the SVG to fill the available width while preserving aspect ratio.
private fun wrapSvgInHtml(svg: String): String {
    return """
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1, user-scalable=no" />
        <style>
            html, body {
                margin: 0;
                padding: 0;
                width: 100%;
                height: 100%;
                background: transparent;
                display: flex;
                align-items: center;
                justify-content: center;
                overflow: hidden;
            }
            svg {
                max-width: 100%;
                max-height: 100%;
                width: auto;
                height: auto;
            }
        </style>
        </head>
        <body>$svg</body>
        </html>
    """.trimIndent()
}

// Local re-import of `remember` to keep the renderer file self-contained without
// dragging composition imports into the public surface above.
@Composable
private inline fun <T> remember(key: Any?, crossinline calc: () -> T): T =
    androidx.compose.runtime.remember(key) { calc() }
