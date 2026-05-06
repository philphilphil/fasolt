import SwiftUI
import Textual

struct CardView: View {
    let label: String
    let text: String
    let sourceFile: String?
    let sourceHeading: String?
    var svg: String? = nil
    var cardId: String? = nil
    var questionText: String? = nil

    @State private var idCopied = false

    var body: some View {
        VStack(spacing: 0) {
            Text(label)
                .font(.caption2)
                .textCase(.uppercase)
                .tracking(1)
                .foregroundStyle(.secondary)
                .padding(.bottom, 8)

            if let questionText {
                StructuredText(markdown: questionText)
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                    .padding(.horizontal, 8)
                    .padding(.bottom, 8)
            }

            if let svg {
                SvgView(svg: svg)
                    .frame(maxWidth: .infinity)
                    .frame(height: 240)
                    .padding(.bottom, 8)
            }

            ScrollView {
                StructuredText(markdown: text)
                    .font(.title3)
                    .foregroundStyle(.primary)
                    .padding(.horizontal, 8)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .scrollBounceBehavior(.basedOnSize)

            VStack(alignment: .leading, spacing: 2) {
                if let sourceFile {
                    HStack(spacing: 4) {
                        Text(sourceFile)
                        if let sourceHeading {
                            Text("\u{00B7}")
                            Text(sourceHeading)
                        }
                    }
                }
                if let cardId {
                    Button {
                        UIPasteboard.general.string = cardId
                        idCopied = true
                        DispatchQueue.main.asyncAfter(deadline: .now() + 2) {
                            idCopied = false
                        }
                    } label: {
                        Text(idCopied ? "Copied!" : cardId)
                            .monospaced()
                    }
                    .buttonStyle(.plain)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .font(.caption2)
            .foregroundStyle(.tertiary)
            .padding(.top, 8)
        }
        .frame(maxWidth: .infinity)
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 16))
        .overlay(
            RoundedRectangle(cornerRadius: 16)
                .strokeBorder(.quaternary, lineWidth: 1)
        )
    }
}

#Preview("Question") {
    CardView(
        label: "Question",
        text: "What organelle is responsible for producing ATP?",
        sourceFile: "biology-101.md",
        sourceHeading: "Cell Structure"
    )
    .padding()
    .background(.black)
}

#Preview("With SVG") {
    CardView(
        label: "Question",
        text: "What shape is this?",
        sourceFile: nil,
        sourceHeading: nil,
        svg: "<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"50\" cy=\"50\" r=\"40\" fill=\"blue\"/></svg>"
    )
    .padding()
    .background(.black)
}
