import SwiftUI
import Textual

struct CardDetailSheet: View {
    let card: any CardDisplayable
    var deckNames: [String]?
    let onDismiss: () -> Void

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: 24) {
                    VStack(spacing: 8) {
                        Text("Front")
                            .font(.caption2)
                            .textCase(.uppercase)
                            .tracking(1)
                            .foregroundStyle(.secondary)
                        if let svg = card.frontSvg, !svg.isEmpty {
                            SvgView(svg: svg)
                                .frame(height: 200)
                        }
                        StructuredText(markdown: card.front)
                            .font(.title3)
                    }

                    Divider()

                    VStack(spacing: 8) {
                        Text("Back")
                            .font(.caption2)
                            .textCase(.uppercase)
                            .tracking(1)
                            .foregroundStyle(.secondary)
                        if let svg = card.backSvg, !svg.isEmpty {
                            SvgView(svg: svg)
                                .frame(height: 200)
                        }
                        StructuredText(markdown: card.back)
                            .font(.title3)
                    }

                    Divider()

                    VStack(spacing: 8) {
                        Text("Scheduling")
                            .font(.caption2)
                            .textCase(.uppercase)
                            .tracking(1)
                            .foregroundStyle(.secondary)

                        LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 12) {
                            FSRSItem(label: "State", value: card.state.capitalized)
                            FSRSItem(label: "Due", value: formatISODate(card.dueAt) ?? "Not scheduled")
                            FSRSItem(label: "Stability", value: card.stability.map { String(format: "%.1f", $0) } ?? "\u{2014}")
                            FSRSItem(label: "Difficulty", value: card.difficulty.map { String(format: "%.1f", $0) } ?? "\u{2014}")
                            FSRSItem(label: "Step", value: card.step.map { "\($0)" } ?? "\u{2014}")
                            FSRSItem(label: "Last Review", value: formatISODate(card.lastReviewedAt) ?? "Never")
                        }
                    }

                    if let deckNames, !deckNames.isEmpty {
                        Divider()
                        HStack(spacing: 4) {
                            Image(systemName: "rectangle.stack")
                            Text(deckNames.joined(separator: ", "))
                        }
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    }

                    if let sourceFile = card.sourceFile {
                        Divider()
                        HStack(spacing: 4) {
                            Image(systemName: "doc.text")
                            Text(sourceFile)
                            if let heading = card.sourceHeading {
                                Text("\u{00B7}")
                                Text(heading)
                            }
                        }
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    }
                }
                .padding(24)
            }
            .navigationTitle("Card Detail")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Done") { onDismiss() }
                }
            }
        }
        .presentationDetents([.medium, .large])
    }
}
