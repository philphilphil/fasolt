import SwiftUI
import UIKit

struct McpSetupSection: View {
    let serverURL: String
    @State private var copiedItem: String?

    private var mcpURL: String {
        "\(serverURL)/mcp"
    }

    var body: some View {
        Section {
            Text(
                "Connect your AI agent to create flashcards from your notes. Copy your MCP URL and add it to your client."
            )
            .font(.system(size: 14))
            .foregroundStyle(FasoltTheme.ink2)

            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    CapsLabel(text: "Your MCP URL", size: 12)
                    Text(mcpURL)
                        .font(.system(size: 15, design: .monospaced))
                        .foregroundStyle(FasoltTheme.ink0)
                        .textSelection(.enabled)
                }
                Spacer()
                copyButton(text: mcpURL, id: "url")
            }

            DisclosureGroup {
                VStack(alignment: .leading, spacing: 8) {
                    Text("Run in your terminal:")
                        .font(.system(size: 12))
                        .foregroundStyle(FasoltTheme.ink2)
                    codeBlock(
                        "claude mcp add fasolt --transport http \(mcpURL)",
                        id: "claude-code"
                    )
                }
                .padding(.vertical, 4)
            } label: {
                Label("Claude Code", systemImage: "terminal")
            }

            DisclosureGroup {
                VStack(alignment: .leading, spacing: 8) {
                    Text("1. Go to Customize → Connectors")
                    Text("2. Tap + then Add Custom Connector")
                    Text("3. Paste your MCP URL")
                    Text("4. Authorize with your Fasolt account")
                    Link(
                        "See documentation",
                        destination: URL(
                            string:
                                "https://support.anthropic.com/en/articles/11175166-getting-started-with-custom-connectors-using-remote-mcp"
                        )!
                    )
                    .font(.system(size: 12, weight: .semibold))
                    .tint(FasoltTheme.accentText)
                }
                .font(.system(size: 15))
                .foregroundStyle(FasoltTheme.ink1)
                .padding(.vertical, 4)
            } label: {
                Label("Claude.ai Web", systemImage: "globe")
            }

            DisclosureGroup {
                VStack(alignment: .leading, spacing: 8) {
                    Text("Requires Pro, Team, Enterprise, or Edu plan.")
                        .font(.system(size: 12))
                        .foregroundStyle(FasoltTheme.ink2)
                    Text("1. Enable Developer Mode in Settings → Apps → Advanced Settings")
                    Text("2. Click Create App")
                    Text("3. Paste your MCP URL")
                    Text("4. Authorize with your Fasolt account")
                    Link(
                        "See documentation",
                        destination: URL(
                            string:
                                "https://help.openai.com/en/articles/12584461-developer-mode-and-mcp-apps-in-chatgpt-beta"
                        )!
                    )
                    .font(.system(size: 12, weight: .semibold))
                    .tint(FasoltTheme.accentText)
                }
                .font(.system(size: 15))
                .foregroundStyle(FasoltTheme.ink1)
                .padding(.vertical, 4)
            } label: {
                Label("ChatGPT", systemImage: "bubble.left.and.bubble.right")
            }

            DisclosureGroup {
                VStack(alignment: .leading, spacing: 8) {
                    HStack(spacing: 0) {
                        Text("1. Open Le Chat → Intelligence → ")
                        Link(
                            "Connectors",
                            destination: URL(string: "https://chat.mistral.ai/connections")!
                        )
                        .tint(FasoltTheme.accentText)
                    }
                    Text("2. Click + Add Connector → Custom MCP Connector")
                    Text("3. Set Connector name to fasolt and paste the server URL:")
                    codeBlock(mcpURL, id: "mistral")
                    Text("4. Click Connect and authorize with your fasolt account")
                    Link(
                        "See documentation",
                        destination: URL(
                            string:
                                "https://docs.mistral.ai/le-chat/knowledge-integrations/connectors/mcp-connectors/"
                        )!
                    )
                    .font(.system(size: 12, weight: .semibold))
                    .tint(FasoltTheme.accentText)
                }
                .font(.system(size: 15))
                .foregroundStyle(FasoltTheme.ink1)
                .padding(.vertical, 4)
            } label: {
                Label("Mistral Le Chat", systemImage: "sparkles")
            }

            DisclosureGroup {
                VStack(alignment: .leading, spacing: 8) {
                    Text("Add to ~/.copilot/mcp-config.json:")
                        .font(.system(size: 12))
                        .foregroundStyle(FasoltTheme.ink2)
                    let configJSON = """
                        {
                          "mcpServers": {
                            "fasolt": {
                              "type": "http",
                              "url": "\(mcpURL)"
                            }
                          }
                        }
                        """
                    codeBlock(configJSON, id: "copilot", alignment: .top)
                }
                .padding(.vertical, 4)
            } label: {
                Label("GitHub Copilot CLI", systemImage: "chevron.left.forwardslash.chevron.right")
            }

            Text("You'll be asked to log in when your AI client first connects.")
                .font(.system(size: 12))
                .foregroundStyle(FasoltTheme.ink2)
        } header: {
            Text("MCP setup")
                .sectionLabel()
        }
    }

    /// Monospaced code/config block on a sunken paper surface with a flat copy button.
    private func codeBlock(
        _ text: String,
        id: String,
        alignment: VerticalAlignment = .center
    ) -> some View {
        HStack(alignment: alignment, spacing: 8) {
            Text(text)
                .font(.system(size: 12, design: .monospaced))
                .foregroundStyle(FasoltTheme.ink0)
                .textSelection(.enabled)
            Spacer(minLength: 8)
            copyButton(text: text, id: id)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .background(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .fill(FasoltTheme.paper2)
        )
    }

    private func copyButton(text: String, id: String) -> some View {
        Button {
            UIPasteboard.general.string = text
            UIImpactFeedbackGenerator(style: .light).impactOccurred()
            withAnimation {
                copiedItem = id
            }
            Task {
                try? await Task.sleep(for: .seconds(2))
                withAnimation {
                    if copiedItem == id {
                        copiedItem = nil
                    }
                }
            }
        } label: {
            Image(systemName: copiedItem == id ? "checkmark" : "doc.on.doc")
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(copiedItem == id ? FasoltTheme.good : FasoltTheme.accentText)
        }
        .buttonStyle(.borderless)
    }
}

#Preview {
    List {
        McpSetupSection(serverURL: "https://fasolt.app")
    }
}
