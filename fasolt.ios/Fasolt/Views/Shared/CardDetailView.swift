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
                    Text("Front")
                        .font(.caption2)
                        .textCase(.uppercase)
                        .tracking(1)
                        .foregroundStyle(.secondary)

                    if let svg = card.frontSvg, !svg.isEmpty {
                        SvgView(svg: svg)
                            .frame(maxWidth: .infinity)
                            .frame(height: 250)
                            .clipShape(RoundedRectangle(cornerRadius: 8))
                    }

                    StructuredText(markdown: card.front)
                        .font(.body)
                }
                .frame(maxWidth: .infinity, alignment: .leading)

                Divider()

                // Back
                VStack(alignment: .leading, spacing: 12) {
                    Text("Back")
                        .font(.caption2)
                        .textCase(.uppercase)
                        .tracking(1)
                        .foregroundStyle(.secondary)

                    if let svg = card.backSvg, !svg.isEmpty {
                        SvgView(svg: svg)
                            .frame(maxWidth: .infinity)
                            .frame(height: 250)
                            .clipShape(RoundedRectangle(cornerRadius: 8))
                    }

                    StructuredText(markdown: card.back)
                        .font(.body)
                }
                .frame(maxWidth: .infinity, alignment: .leading)

                Divider()

                if card.isSuspended {
                    HStack(spacing: 6) {
                        Image(systemName: "pause.circle.fill")
                        Text("Suspended")
                            .font(.subheadline.weight(.medium))
                    }
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
                }

                // Scheduling
                VStack(spacing: 8) {
                    Text("Scheduling")
                        .font(.caption2)
                        .textCase(.uppercase)
                        .tracking(1)
                        .foregroundStyle(.secondary)

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
                    HStack(spacing: 4) {
                        Image(systemName: "rectangle.stack")
                        Text(deckNames.joined(separator: ", "))
                    }
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
                }

                if let sourceFile = card.sourceFile {
                    if deckNames == nil || deckNames?.isEmpty == true {
                        Divider()
                    }
                    HStack(spacing: 4) {
                        Image(systemName: "doc.text")
                        Text(sourceFile)
                        if let heading = card.sourceHeading {
                            Text("·")
                            Text(heading)
                        }
                    }
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
            .padding()
        }
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
                    sourceHeading: card.sourceHeading,
                    deckIds: currentDeckIds,
                    isSuspended: card.isSuspended
                ),
                decks: availableDecks,
                onSave: { request, deckIds in
                    let updateRequest = UpdateCardRequest(
                        front: request.front,
                        back: request.back,
                        sourceFile: request.sourceFile,
                        sourceHeading: request.sourceHeading,
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
