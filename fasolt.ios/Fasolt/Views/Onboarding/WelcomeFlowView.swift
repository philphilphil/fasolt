import SwiftUI

struct WelcomeFlowView: View {
    @Environment(\.dismiss) private var dismiss
    @Environment(AuthService.self) private var authService
    @AppStorage("hasSeenWelcomeFlow") private var hasSeenWelcomeFlow = false
    @State private var currentStep = 0

    private let totalSteps = 4

    var body: some View {
        NavigationStack {
            VStack(spacing: 0) {
                TabView(selection: $currentStep) {
                    HowItWorksStep().tag(0)
                    WhatYouNeedStep().tag(1)
                    ConnectAIStep(serverURL: authService.serverURL).tag(2)
                    YouAreSetStep().tag(3)
                }
                .tabViewStyle(.page(indexDisplayMode: .always))
                .indexViewStyle(.page(backgroundDisplayMode: .always))

                Button {
                    if currentStep < totalSteps - 1 {
                        withAnimation { currentStep += 1 }
                    } else {
                        complete()
                    }
                } label: {
                    Text(buttonTitle)
                        .frame(maxWidth: .infinity)
                        .frame(height: 24)
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .padding(.horizontal)
                .padding(.bottom, 8)
            }
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                if currentStep < totalSteps - 1 {
                    ToolbarItem(placement: .topBarTrailing) {
                        Button("Skip") { complete() }
                            .foregroundStyle(.secondary)
                    }
                }
            }
        }
    }

    private var buttonTitle: String {
        switch currentStep {
        case totalSteps - 1: "Open Dashboard"
        default: "Continue"
        }
    }

    private func complete() {
        hasSeenWelcomeFlow = true
        dismiss()
    }
}

// MARK: - Steps

private struct HowItWorksStep: View {
    var body: some View {
        VStack(spacing: 28) {
            Spacer().frame(height: 12)

            Image("FasoltLogo")
                .resizable()
                .aspectRatio(contentMode: .fit)
                .frame(width: 88, height: 88)
                .clipShape(RoundedRectangle(cornerRadius: 20, style: .continuous))

            VStack(spacing: 4) {
                Text("Spaced repetition,")
                    .font(.title.bold())
                Text("powered by your AI.")
                    .font(.title.bold())
                    .foregroundStyle(FasoltTheme.accent)
            }
            .multilineTextAlignment(.center)

            VStack(alignment: .leading, spacing: 18) {
                bulletRow(
                    icon: "sparkles",
                    text: "Your AI creates flashcards. From your notes or from scratch."
                )
                bulletRow(
                    icon: "arrow.triangle.2.circlepath",
                    text: "Cards sync to Fasolt automatically."
                )
                bulletRow(
                    icon: "brain.head.profile",
                    text: "You review them here at the right time, the right amount."
                )
            }
            .padding(.horizontal, 32)

            Spacer()
        }
    }

    private func bulletRow(icon: String, text: String) -> some View {
        HStack(alignment: .top, spacing: 14) {
            Image(systemName: icon)
                .font(.title2)
                .foregroundStyle(FasoltTheme.accent)
                .frame(width: 28, alignment: .center)
            Text(text)
                .font(.body)
                .multilineTextAlignment(.leading)
                .fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 0)
        }
    }
}

private struct WhatYouNeedStep: View {
    var body: some View {
        VStack(spacing: 24) {
            Spacer().frame(height: 12)

            Image(systemName: "wand.and.stars")
                .font(.system(size: 72, weight: .light))
                .foregroundStyle(FasoltTheme.accent)
                .frame(height: 88)

            Text("You'll need an AI client.")
                .font(.title.bold())
                .multilineTextAlignment(.center)
                .padding(.horizontal)

            Text(
                "Fasolt connects to Claude, ChatGPT, GitHub Copilot, or any MCP-compatible client. Your AI does the work of creating and updating cards."
            )
            .font(.body)
            .multilineTextAlignment(.center)
            .foregroundStyle(.secondary)
            .padding(.horizontal, 32)
            .fixedSize(horizontal: false, vertical: true)

            HStack(spacing: 10) {
                clientChip("Claude")
                clientChip("ChatGPT")
                clientChip("Copilot")
            }

            Text("Don't have one yet? You can still try a demo deck after this.")
                .font(.footnote)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
                .padding(.horizontal, 32)
                .fixedSize(horizontal: false, vertical: true)

            Spacer()
        }
    }

    private func clientChip(_ name: String) -> some View {
        Text(name)
            .font(.caption.weight(.medium))
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .background(FasoltTheme.accentSoft, in: Capsule())
            .foregroundStyle(FasoltTheme.accent)
    }
}

private struct ConnectAIStep: View {
    let serverURL: String

    var body: some View {
        VStack(spacing: 0) {
            VStack(spacing: 6) {
                Text("Add Fasolt to your AI client")
                    .font(.title2.bold())
                    .multilineTextAlignment(.center)
                    .padding(.horizontal)
            }
            .padding(.top, 12)
            .padding(.bottom, 4)

            List {
                McpSetupSection(serverURL: serverURL)
            }
            .scrollContentBackground(.hidden)
        }
    }
}

private struct YouAreSetStep: View {
    var body: some View {
        VStack(spacing: 24) {
            Spacer()

            Image(systemName: "checkmark.circle.fill")
                .font(.system(size: 96, weight: .regular))
                .foregroundStyle(.green)
                .symbolRenderingMode(.hierarchical)

            VStack(spacing: 12) {
                Text("You're ready to go.")
                    .font(.title.bold())
                Text(
                    "Start creating cards through your AI client, or try a demo deck on the dashboard."
                )
                .font(.body)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
                .padding(.horizontal, 32)
                .fixedSize(horizontal: false, vertical: true)
            }

            Spacer()
            Spacer()
        }
    }
}

#Preview {
    WelcomeFlowView()
        .environment(AuthService())
}
