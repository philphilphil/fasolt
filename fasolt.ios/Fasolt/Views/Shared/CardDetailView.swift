import SwiftUI
import Textual

struct CardDetailView: View {
    let card: any CardDisplayable
    var deckNames: [String]?
    var currentDeckId: String?
    var availableDecks: [DeckDTO] = []
    var onSaveEdit: ((UpdateCardRequest) async throws -> Void)?
    var onToggleSuspended: ((Bool) async throws -> Void)?

    @State private var showEditSheet = false

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
                        FSRSItem(label: "Step", value: card.step.map { "\($0)" } ?? "—")
                        FSRSItem(label: "Last Review", value: formatISODate(card.lastReviewedAt) ?? "Never")
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
            if onSaveEdit != nil {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        showEditSheet = true
                    } label: {
                        Label("Edit", systemImage: "pencil")
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
                    deckId: currentDeckId,
                    isSuspended: card.isSuspended
                ),
                decks: availableDecks,
                onSave: { request, deckId in
                    let updateRequest = UpdateCardRequest(
                        front: request.front,
                        back: request.back,
                        sourceFile: request.sourceFile,
                        sourceHeading: request.sourceHeading,
                        deckIds: deckId.map { [$0] }
                    )
                    try await onSaveEdit?(updateRequest)
                },
                onToggleSuspended: onToggleSuspended
            )
        }
    }
}
