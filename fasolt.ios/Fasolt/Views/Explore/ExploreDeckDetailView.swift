import SwiftUI
import Textual

/// A shared deck as seen before importing it: metadata, a few sample cards, and the
/// two ways to take it — a copy you own, or a link that follows the author.
struct ExploreDeckDetailView: View {
    @State private var viewModel: ExploreDeckViewModel
    @State private var sampleIndex = 0
    @State private var showUnlinkAlert = false

    init(viewModel: ExploreDeckViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        Group {
            if let deck = viewModel.deck {
                VStack(spacing: 0) {
                    ScrollView {
                        VStack(alignment: .leading, spacing: 20) {
                            header(deck)

                            if !deck.sampleCards.isEmpty {
                                samples(deck)
                            }

                            statusMessages
                        }
                        .padding(.horizontal, FasoltTheme.pagePadding)
                        .padding(.vertical, 16)
                    }

                    actions
                }
            } else if let error = viewModel.errorMessage {
                ContentUnavailableView {
                    Label("Could not load", systemImage: "wifi.slash")
                } description: {
                    Text(error)
                } actions: {
                    Button("Retry") { Task { await viewModel.load() } }
                }
            } else {
                ProgressView()
            }
        }
        .background(FasoltTheme.paper0.ignoresSafeArea())
        .navigationTitle(viewModel.deck?.name ?? "Deck")
        .navigationBarTitleDisplayMode(.inline)
        .offlineBanner()
        .refreshable { await viewModel.load() }
        .task { if viewModel.deck == nil { await viewModel.load() } }
        .alert("Unlink this deck?", isPresented: $showUnlinkAlert) {
            Button("Unlink", role: .destructive) {
                Task { await viewModel.unlinkDeck() }
            }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("\"\(viewModel.deck?.name ?? "This deck")\" is removed from your decks along with "
                 + "your review progress for its cards. The author's deck is not affected, and you "
                 + "can link it again at any time.")
        }
    }

    // MARK: - Header

    private func header(_ deck: LibraryDeckDetailDTO) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 12) {
                DeckInitialsBadge(name: deck.name, color: FasoltTheme.deckColor(for: deck.id), size: 44)

                VStack(alignment: .leading, spacing: 3) {
                    Text(deck.name)
                        .font(.system(size: 20, weight: .semibold))
                        .foregroundStyle(FasoltTheme.ink0)
                    if let handle = deck.authorHandle {
                        Text("@\(handle)")
                            .font(.system(size: 14))
                            .foregroundStyle(FasoltTheme.ink2)
                    }
                }

                Spacer(minLength: 0)

                if deck.visibility == DeckVisibility.unlisted {
                    Text("Unlisted")
                        .font(.system(size: 10, weight: .medium))
                        .padding(.horizontal, 6)
                        .padding(.vertical, 2)
                        .background(FasoltTheme.paper2, in: Capsule())
                        .foregroundStyle(FasoltTheme.ink2)
                }
            }

            if let description = deck.description, !description.isEmpty {
                Text(description)
                    .font(.system(size: 15))
                    .foregroundStyle(FasoltTheme.ink1)
                    .fixedSize(horizontal: false, vertical: true)
            }

            HStack(spacing: 6) {
                Text("\(deck.cardCount) \(deck.cardCount == 1 ? "card" : "cards")")
                if deck.copyCount > 0 {
                    Text("·")
                        .foregroundStyle(FasoltTheme.ink3)
                    Text("\(deck.copyCount) \(deck.copyCount == 1 ? "import" : "imports")")
                }
            }
            .font(.system(size: 13))
            .monospacedDigit()
            .foregroundStyle(FasoltTheme.ink2)
        }
    }

    // MARK: - Sample cards

    private func samples(_ deck: LibraryDeckDetailDTO) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 10) {
                Text("Sample cards")
                    .sectionLabel()
                Spacer()

                if deck.sampleCards.count > 1 {
                    Button {
                        withAnimation(.easeOut(duration: 0.15)) {
                            sampleIndex = (sampleIndex - 1 + deck.sampleCards.count) % deck.sampleCards.count
                        }
                    } label: {
                        Image(systemName: "chevron.left")
                            .font(.system(size: 12, weight: .semibold))
                            .foregroundStyle(FasoltTheme.ink2)
                    }
                    .buttonStyle(.plain)
                }

                Text("\(min(sampleIndex + 1, deck.sampleCards.count)) / \(deck.sampleCards.count)")
                    .font(.system(size: 12))
                    .monospacedDigit()
                    .foregroundStyle(FasoltTheme.ink2)

                if deck.sampleCards.count > 1 {
                    Button {
                        withAnimation(.easeOut(duration: 0.15)) {
                            sampleIndex = (sampleIndex + 1) % deck.sampleCards.count
                        }
                    } label: {
                        Image(systemName: "chevron.right")
                            .font(.system(size: 12, weight: .semibold))
                            .foregroundStyle(FasoltTheme.ink2)
                    }
                    .buttonStyle(.plain)
                }
            }

            let clampedIndex = min(sampleIndex, deck.sampleCards.count - 1)
            sampleCard(deck.sampleCards[clampedIndex])
                .id(clampedIndex)
        }
    }

    /// Shows a sample card's front and back at once — no tap-to-flip.
    private func sampleCard(_ card: LibrarySampleCardDTO) -> some View {
        ZStack(alignment: .top) {
            RoundedRectangle(cornerRadius: FasoltTheme.cardRadius, style: .continuous)
                .fill(FasoltTheme.paper1)
                .overlay(
                    RoundedRectangle(cornerRadius: FasoltTheme.cardRadius, style: .continuous)
                        .strokeBorder(FasoltTheme.rule2, lineWidth: FasoltTheme.hairline)
                )
                .shadow(color: .black.opacity(0.04), radius: 8, x: 0, y: 2)

            VStack(spacing: 0) {
                AccentStripe(horizontalInset: 24)

                VStack(alignment: .leading, spacing: 0) {
                    CapsLabel(text: "Front", color: FasoltTheme.accent, size: 11)
                        .padding(.horizontal, 22)
                        .padding(.top, 20)
                        .padding(.bottom, 10)

                    if let frontSvg = card.frontSvg {
                        SvgView(svg: frontSvg)
                            .frame(maxWidth: .infinity)
                            .frame(height: 160)
                            .padding(.horizontal, 16)
                            .padding(.bottom, 8)
                    }

                    StructuredText(markdown: card.front)
                        .font(.system(size: 18, weight: .semibold))
                        .foregroundStyle(FasoltTheme.ink0)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal, 22)
                        .padding(.bottom, 16)

                    Rectangle()
                        .fill(FasoltTheme.rule2)
                        .frame(height: FasoltTheme.hairline)
                        .padding(.horizontal, 22)

                    CapsLabel(text: "Back", color: FasoltTheme.ink2, size: 11)
                        .padding(.horizontal, 22)
                        .padding(.top, 16)
                        .padding(.bottom, 10)

                    if let backSvg = card.backSvg {
                        SvgView(svg: backSvg)
                            .frame(maxWidth: .infinity)
                            .frame(height: 160)
                            .padding(.horizontal, 16)
                            .padding(.bottom, 8)
                    }

                    StructuredText(markdown: card.back)
                        .font(.system(size: 16))
                        .foregroundStyle(FasoltTheme.ink0)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal, 22)
                        .padding(.bottom, 20)
                }
            }
        }
    }

    // MARK: - Status

    @ViewBuilder
    private var statusMessages: some View {
        if let copied = viewModel.copiedDeck {
            statusLine(
                icon: "checkmark.circle.fill",
                text: viewModel.copiedFromLink
                    ? "Converted to “\(copied.name)” — your review progress is preserved and the link is gone."
                    : "Copied as “\(copied.name)” — your own cards, your own progress.",
                color: FasoltTheme.good
            )
        }

        switch viewModel.relation {
        case .linked:
            statusLine(
                icon: "link",
                text: "Linked — this deck follows the author's updates and appears in your decks.",
                color: FasoltTheme.accentText
            )
        case .owner:
            statusLine(
                icon: "person.crop.circle",
                text: "This is your deck.",
                color: FasoltTheme.ink2
            )
        case .notImported:
            EmptyView()
        }

        if let importError = viewModel.importError {
            statusLine(icon: "exclamationmark.triangle.fill", text: importError, color: FasoltTheme.again)
        }
    }

    private func statusLine(icon: String, text: String, color: Color) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: 6) {
            Image(systemName: icon)
                .font(.system(size: 12))
            Text(text)
                .font(.system(size: 13))
                .fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 0)
        }
        .foregroundStyle(color)
    }

    // MARK: - Actions

    @ViewBuilder
    private var actions: some View {
        if viewModel.relation != .owner {
            VStack(spacing: 8) {
                Button {
                    Task { await viewModel.copyToMyDecks() }
                } label: {
                    Text(viewModel.relation == .linked
                         ? (viewModel.importingMode == .copy ? "Converting…" : "Convert to copy")
                         : (viewModel.importingMode == .copy ? "Copying…" : "Copy to my decks"))
                }
                .buttonStyle(AccentButtonStyle())
                .disabled(viewModel.isBusy)

                if viewModel.relation == .linked {
                    // The link was made here, so it can be undone here.
                    Button {
                        showUnlinkAlert = true
                    } label: {
                        Text(viewModel.isUnlinking ? "Unlinking…" : "Unlink deck")
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(SoftAccentButtonStyle())
                    .disabled(viewModel.isBusy)
                } else {
                    Button {
                        Task { await viewModel.linkDeck() }
                    } label: {
                        Text(viewModel.importingMode == .link ? "Linking…" : "Link deck")
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(SoftAccentButtonStyle())
                    .disabled(viewModel.isBusy)

                    Text("A copy is yours to edit. A link stays in sync with the author.")
                        .font(.system(size: 12))
                        .foregroundStyle(FasoltTheme.ink2)
                        .multilineTextAlignment(.center)
                }
            }
            .padding(.horizontal, FasoltTheme.pagePadding)
            .padding(.vertical, 12)
            .background(FasoltTheme.paper0)
        }
    }
}
