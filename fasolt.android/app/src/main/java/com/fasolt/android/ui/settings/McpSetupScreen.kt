package com.fasolt.android.ui.settings

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.KeyboardArrowUp
import androidx.compose.material.icons.outlined.ContentCopy
import androidx.compose.material.icons.automirrored.outlined.Chat
import androidx.compose.material.icons.automirrored.outlined.OpenInNew
import androidx.compose.material.icons.outlined.Terminal
import androidx.compose.material.icons.outlined.Code
import androidx.compose.material.icons.outlined.Language
import androidx.compose.material.icons.outlined.AutoAwesome
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import com.fasolt.android.FasoltApplication
import kotlinx.coroutines.delay

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun McpSetupScreen(onBack: () -> Unit) {
    val context = LocalContext.current
    val app = context.applicationContext as FasoltApplication
    val serverUrl = remember { app.secureStorage.serverUrl?.trimEnd('/').orEmpty() }
    val mcpUrl = remember(serverUrl) { if (serverUrl.isEmpty()) "" else "$serverUrl/mcp" }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("MCP setup") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Text(
                "Connect your AI agent to create flashcards from your notes. Copy your MCP URL and add it to your client.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )

            if (mcpUrl.isEmpty()) {
                Text(
                    "Server URL is not set. Sign out and sign back in pointed at your server.",
                    color = MaterialTheme.colorScheme.error,
                    style = MaterialTheme.typography.bodyMedium,
                )
                return@Column
            }

            McpUrlCard(mcpUrl = mcpUrl, context = context)

            val claudeCodeCommand = "claude mcp add fasolt --transport http $mcpUrl"
            ExpandableClientCard(
                title = "Claude Code",
                icon = Icons.Outlined.Terminal,
            ) {
                Text(
                    "Run in your terminal:",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Spacer(Modifier.height(8.dp))
                CopyableSnippet(value = claudeCodeCommand, label = "Claude Code", context = context)
            }

            ExpandableClientCard(
                title = "Claude.ai Web",
                icon = Icons.Outlined.Language,
            ) {
                NumberedSteps(
                    steps = listOf(
                        "Go to Customize → Connectors",
                        "Tap + then Add Custom Connector",
                        "Paste your MCP URL",
                        "Authorize with your Fasolt account",
                    ),
                )
                Spacer(Modifier.height(8.dp))
                DocsLink(
                    url = "https://support.anthropic.com/en/articles/11175166-getting-started-with-custom-connectors-using-remote-mcp",
                    context = context,
                )
            }

            ExpandableClientCard(
                title = "ChatGPT",
                icon = Icons.AutoMirrored.Outlined.Chat,
            ) {
                Text(
                    "Requires Pro, Team, Enterprise, or Edu plan.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Spacer(Modifier.height(8.dp))
                NumberedSteps(
                    steps = listOf(
                        "Enable Developer Mode in Settings → Apps → Advanced Settings",
                        "Click Create App",
                        "Paste your MCP URL",
                        "Authorize with your Fasolt account",
                    ),
                )
                Spacer(Modifier.height(8.dp))
                DocsLink(
                    url = "https://help.openai.com/en/articles/12584461-developer-mode-and-mcp-apps-in-chatgpt-beta",
                    context = context,
                )
            }

            ExpandableClientCard(
                title = "Mistral Le Chat",
                icon = Icons.Outlined.AutoAwesome,
            ) {
                NumberedSteps(
                    steps = listOf(
                        "Open Le Chat → Intelligence → Connectors",
                        "Click + Add Connector → Custom MCP Connector",
                        "Set Connector name to fasolt and paste the server URL below",
                        "Click Connect and authorize with your Fasolt account",
                    ),
                )
                Spacer(Modifier.height(8.dp))
                CopyableSnippet(value = mcpUrl, label = "Mistral URL", context = context)
                Spacer(Modifier.height(8.dp))
                DocsLink(
                    url = "https://docs.mistral.ai/le-chat/knowledge-integrations/connectors/mcp-connectors/",
                    context = context,
                )
            }

            val copilotConfig = """
                {
                  "mcpServers": {
                    "fasolt": {
                      "type": "http",
                      "url": "$mcpUrl"
                    }
                  }
                }
            """.trimIndent()
            ExpandableClientCard(
                title = "GitHub Copilot CLI",
                icon = Icons.Outlined.Code,
            ) {
                Text(
                    "Add to ~/.copilot/mcp-config.json:",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Spacer(Modifier.height(8.dp))
                CopyableSnippet(value = copilotConfig, label = "Copilot config", context = context)
            }

            Text(
                "You'll be asked to log in when your AI client first connects.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun McpUrlCard(mcpUrl: String, context: Context) {
    ElevatedCard(modifier = Modifier.fillMaxWidth()) {
        Column(Modifier.padding(16.dp)) {
            Text(
                "Your MCP URL",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(4.dp))
            CopyableSnippet(value = mcpUrl, label = "MCP URL", context = context)
        }
    }
}

@Composable
private fun ExpandableClientCard(
    title: String,
    icon: ImageVector,
    content: @Composable () -> Unit,
) {
    var expanded by remember { mutableStateOf(false) }
    ElevatedCard(modifier = Modifier.fillMaxWidth()) {
        Column {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clickable { expanded = !expanded }
                    .padding(horizontal = 16.dp, vertical = 14.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Icon(
                    icon,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                )
                Spacer(Modifier.height(0.dp))
                Text(
                    title,
                    style = MaterialTheme.typography.titleSmall,
                    modifier = Modifier
                        .weight(1f)
                        .padding(start = 12.dp),
                )
                Icon(
                    if (expanded) Icons.Default.KeyboardArrowUp else Icons.Default.KeyboardArrowDown,
                    contentDescription = if (expanded) "Collapse" else "Expand",
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            AnimatedVisibility(visible = expanded) {
                Column(Modifier.padding(start = 16.dp, end = 16.dp, bottom = 16.dp)) {
                    content()
                }
            }
        }
    }
}

@Composable
private fun NumberedSteps(steps: List<String>) {
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        steps.forEachIndexed { index, step ->
            Text(
                "${index + 1}. $step",
                style = MaterialTheme.typography.bodyMedium,
            )
        }
    }
}

@Composable
private fun CopyableSnippet(value: String, label: String, context: Context) {
    var copied by remember { mutableStateOf(false) }
    LaunchedEffect(copied) {
        if (copied) {
            delay(2000)
            copied = false
        }
    }

    Row(verticalAlignment = Alignment.Top) {
        Surface(
            color = MaterialTheme.colorScheme.surfaceVariant,
            shape = MaterialTheme.shapes.small,
            modifier = Modifier.weight(1f),
        ) {
            Text(
                value,
                style = MaterialTheme.typography.bodySmall,
                fontFamily = FontFamily.Monospace,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(12.dp),
            )
        }
        IconButton(onClick = {
            copyToClipboard(context, label = label, text = value)
            copied = true
        }) {
            Icon(
                imageVector = if (copied) Icons.Default.Check else Icons.Outlined.ContentCopy,
                contentDescription = if (copied) "Copied" else "Copy",
                tint = if (copied) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun DocsLink(url: String, context: Context) {
    TextButton(
        onClick = {
            runCatching {
                context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
            }
        },
        contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 0.dp),
    ) {
        Icon(
            Icons.AutoMirrored.Outlined.OpenInNew,
            contentDescription = null,
            modifier = Modifier.height(16.dp),
        )
        Spacer(Modifier.height(0.dp))
        Text(
            "See documentation",
            style = MaterialTheme.typography.bodySmall,
            modifier = Modifier.padding(start = 4.dp),
        )
    }
}

private fun copyToClipboard(context: Context, label: String, text: String) {
    val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
    clipboard.setPrimaryClip(ClipData.newPlainText(label, text))
}
