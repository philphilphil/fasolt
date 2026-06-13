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
                        CapsLabel(text: "Front")
                        if let svg = card.frontSvg, !svg.isEmpty {
                            SvgView(svg: svg)
                                .frame(height: 200)
                        }
                        StructuredText(markdown: card.front)
                            .font(.title3)
                            .foregroundStyle(FasoltTheme.ink0)
                    }

                    Divider()

                    VStack(spacing: 8) {
                        CapsLabel(text: "Back")
                        if let svg = card.backSvg, !svg.isEmpty {
                            SvgView(svg: svg)
                                .frame(height: 200)
                        }
                        StructuredText(markdown: card.back)
                            .font(.title3)
                            .foregroundStyle(FasoltTheme.ink0)
                    }

                    Divider()

                    VStack(spacing: 8) {
                        CapsLabel(text: "Scheduling")

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
                        .foregroundStyle(FasoltTheme.ink2)
                    }

                    if let sourceFile = card.sourceFile {
                        Divider()
                        HStack(spacing: 4) {
                            Image(systemName: "doc.text")
                            Text(sourceFile)
                        }
                        .font(.caption)
                        .foregroundStyle(FasoltTheme.ink2)
                    }
                }
                .padding(24)
            }
            .background(FasoltTheme.paper0.ignoresSafeArea())
            .scrollContentBackground(.hidden)
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
