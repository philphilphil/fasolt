import SwiftUI
import Textual

struct CardDetailView: View {
    let card: any CardDisplayable
    var deckNames: [String]?
    var currentDeckIds: [String] = []
    var availableDecks: [DeckDTO] = []
    var onSaveEdit: ((UpdateCardRequest) async throws -> Void)?
    var onToggleSuspended: ((Bool) async throws -> Void)?
    var onDelete: (() async throws -> Void)?
    var showsToolbarActions: Bool = true

    @Environment(\.dismiss) private var dismiss
    @State private var showEditSheet = false
    @State private var showDeleteAlert = false
    @State private var errorMessage: String?

    var body: some View {
        ScrollView {
            VStack(spacing: 24) {
                // Front
                VStack(alignment: .leading, spacing: 12) {
                    CapsLabel(text: "Front")

                    if let svg = card.frontSvg, !svg.isEmpty {
                        SvgView(svg: svg)
                            .frame(maxWidth: .infinity)
                            .frame(height: 250)
                            .clipShape(RoundedRectangle(cornerRadius: 8))
                    }

                    StructuredText(markdown: card.front)
                        .font(.body)
                        .foregroundStyle(FasoltTheme.ink0)
                }
                .frame(maxWidth: .infinity, alignment: .leading)

                Divider()
                    .overlay(FasoltTheme.rule1)

                // Back
                VStack(alignment: .leading, spacing: 12) {
                    CapsLabel(text: "Back")

                    if let svg = card.backSvg, !svg.isEmpty {
                        SvgView(svg: svg)
                            .frame(maxWidth: .infinity)
                            .frame(height: 250)
                            .clipShape(RoundedRectangle(cornerRadius: 8))
                    }

                    StructuredText(markdown: card.back)
                        .font(.body)
                        .foregroundStyle(FasoltTheme.ink0)
                }
                .frame(maxWidth: .infinity, alignment: .leading)

                Divider()
                    .overlay(FasoltTheme.rule1)

                if card.isSuspended {
                    HStack(spacing: 6) {
                        Image(systemName: "pause.circle.fill")
                        Text("Suspended")
                            .font(.subheadline.weight(.medium))
                    }
                    .foregroundStyle(FasoltTheme.ink2)
                    .frame(maxWidth: .infinity, alignment: .leading)
                }

                // Scheduling
                VStack(spacing: 8) {
                    CapsLabel(text: "Scheduling")
                        .frame(maxWidth: .infinity, alignment: .leading)

                    LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible()), GridItem(.flexible())], spacing: 12) {
                        FSRSItem(label: "State", value: card.state.capitalized)
                        FSRSItem(label: "Due", value: formatISODate(card.dueAt) ?? "—")
                        FSRSItem(label: "Stability", value: card.stability.map { String(format: "%.1f", $0) } ?? "—")
                        FSRSItem(label: "Difficulty", value: card.difficulty.map { String(format: "%.1f", $0) } ?? "—")
                        FSRSItem(label: "Last Review", value: formatISODate(card.lastReviewedAt) ?? "Never")
                        // Step only applies during learning/relearning; hide otherwise to avoid a permanent "—"
                        if let step = card.step, card.state == "learning" || card.state == "relearning" {
                            FSRSItem(label: "Step", value: "\(step)")
                        }
                    }
                }

                // Metadata
                if let deckNames, !deckNames.isEmpty {
                    Divider()
                        .overlay(FasoltTheme.rule1)
                    HStack(spacing: 4) {
                        Image(systemName: "rectangle.stack")
                        Text(deckNames.joined(separator: ", "))
                    }
                    .font(.caption)
                    .foregroundStyle(FasoltTheme.ink2)
                    .frame(maxWidth: .infinity, alignment: .leading)
                }

                if let sourceFile = card.sourceFile {
                    if deckNames == nil || deckNames?.isEmpty == true {
                        Divider()
                            .overlay(FasoltTheme.rule1)
                    }
                    HStack(spacing: 4) {
                        Image(systemName: "doc.text")
                        Text(sourceFile)
                    }
                    .font(.caption)
                    .foregroundStyle(FasoltTheme.ink2)
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
            .padding()
        }
        .background(FasoltTheme.paper0.ignoresSafeArea())
        .scrollContentBackground(.hidden)
        .navigationTitle("Card")
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            if showsToolbarActions {
                if onSaveEdit != nil {
                    ToolbarItem(placement: .topBarTrailing) {
                        Button {
                            showEditSheet = true
                        } label: {
                            Label("Edit", systemImage: "pencil")
                        }
                    }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    Menu {
                        if onToggleSuspended != nil {
                            Button {
                                Task { await toggleSuspended() }
                            } label: {
                                Label(
                                    card.isSuspended ? "Unsuspend" : "Suspend",
                                    systemImage: card.isSuspended ? "play.circle" : "pause.circle"
                                )
                            }
                        }

                        Button {
                            UIPasteboard.general.string = card.id
                            UIImpactFeedbackGenerator(style: .light).impactOccurred()
                        } label: {
                            Label("Copy ID", systemImage: "doc.on.doc")
                        }

                        if onDelete != nil {
                            Divider()

                            Button(role: .destructive) {
                                showDeleteAlert = true
                            } label: {
                                Label("Delete", systemImage: "trash")
                            }
                        }
                    } label: {
                        Label("More", systemImage: "ellipsis.circle")
                    }
                }
            }
        }
        .sheet(isPresented: $showEditSheet) {
            CardFormSheet(
                mode: .edit(
                    front: card.front,
                    back: card.back,
                    sourceFile: card.sourceFile,
                    deckIds: currentDeckIds,
                    isSuspended: card.isSuspended
                ),
                decks: availableDecks,
                onSave: { request, deckIds in
                    let updateRequest = UpdateCardRequest(
                        front: request.front,
                        back: request.back,
                        sourceFile: request.sourceFile,
                        deckIds: deckIds
                    )
                    try await onSaveEdit?(updateRequest)
                },
                onToggleSuspended: onToggleSuspended
            )
        }
        .alert("Delete Card", isPresented: $showDeleteAlert) {
            Button("Delete", role: .destructive) {
                Task { await delete() }
            }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("This cannot be undone.")
        }
        .alert("Error", isPresented: .init(get: { errorMessage != nil }, set: { if !$0 { errorMessage = nil } })) {
            Button("OK") { errorMessage = nil }
        } message: {
            Text(errorMessage ?? "")
        }
    }

    private func toggleSuspended() async {
        do {
            try await onToggleSuspended?(!card.isSuspended)
        } catch {
            errorMessage = "Could not update card."
        }
    }

    private func delete() async {
        do {
            try await onDelete?()
            dismiss()
        } catch {
            errorMessage = "Could not delete card."
        }
    }
}
