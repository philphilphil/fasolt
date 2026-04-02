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
            Text("Connect your AI agent to create flashcards from your notes. Copy your MCP URL and add it to your client.")
                .font(.subheadline)
                .foregroundStyle(.secondary)

            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("Your MCP URL")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Text(mcpURL)
                        .font(.subheadline.monospaced())
                }
                Spacer()
                copyButton(text: mcpURL, id: "url")
            }

            DisclosureGroup {
                VStack(alignment: .leading, spacing: 8) {
                    Text("Run in your terminal:")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    HStack {
                        Text("claude mcp add fasolt --transport http \(mcpURL)")
                            .font(.caption.monospaced())
                            .textSelection(.enabled)
                        Spacer()
                        copyButton(
                            text: "claude mcp add fasolt --transport http \(mcpURL)",
                            id: "claude-code"
                        )
                    }
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
                    Link("See documentation",
                         destination: URL(string: "https://support.anthropic.com/en/articles/11175166-getting-started-with-custom-connectors-using-remote-mcp")!)
                        .font(.caption)
                }
                .font(.subheadline)
                .padding(.vertical, 4)
            } label: {
                Label("Claude.ai Web", systemImage: "globe")
            }

            DisclosureGroup {
                VStack(alignment: .leading, spacing: 8) {
                    Text("Requires Pro, Team, Enterprise, or Edu plan.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Text("1. Enable Developer Mode in Settings → Apps → Advanced Settings")
                    Text("2. Click Create App")
                    Text("3. Paste your MCP URL")
                    Text("4. Authorize with your Fasolt account")
                    Link("See documentation",
                         destination: URL(string: "https://help.openai.com/en/articles/12584461-developer-mode-and-mcp-apps-in-chatgpt-beta")!)
                        .font(.caption)
                }
                .font(.subheadline)
                .padding(.vertical, 4)
            } label: {
                Label("ChatGPT", systemImage: "bubble.left.and.bubble.right")
            }

            DisclosureGroup {
                VStack(alignment: .leading, spacing: 8) {
                    Text("Add to ~/.copilot/mcp-config.json:")
                        .font(.caption)
                        .foregroundStyle(.secondary)
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
                    HStack(alignment: .top) {
                        Text(configJSON)
                            .font(.caption.monospaced())
                            .textSelection(.enabled)
                        Spacer()
                        copyButton(text: configJSON, id: "copilot")
                    }
                }
                .padding(.vertical, 4)
            } label: {
                Label("GitHub Copilot CLI", systemImage: "chevron.left.forwardslash.chevron.right")
            }

            Text("You'll be asked to log in when your AI client first connects.")
                .font(.caption)
                .foregroundStyle(.secondary)
        } header: {
            Text("MCP Setup")
        }
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
                .font(.caption)
                .foregroundStyle(copiedItem == id ? .green : .accentColor)
        }
        .buttonStyle(.borderless)
    }
}

#Preview {
    List {
        McpSetupSection(serverURL: "https://fasolt.app")
    }
}
