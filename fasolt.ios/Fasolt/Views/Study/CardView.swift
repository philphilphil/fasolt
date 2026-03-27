import SwiftUI
import Textual

struct CardView: View {
    let label: String
    let text: String
    let sourceFile: String?
    let sourceHeading: String?
    var svg: String? = nil

    var body: some View {
        VStack(spacing: 0) {
            Spacer()

            Text(label)
                .font(.caption2)
                .textCase(.uppercase)
                .tracking(1)
                .foregroundStyle(.secondary)
                .padding(.bottom, 12)

            if let svg {
                SvgView(svg: svg)
                    .frame(maxWidth: .infinity)
                    .frame(height: 300)
                    .padding(.bottom, 8)
            }

            ScrollView {
                StructuredText(markdown: text)
                    .font(.title3)
                    .foregroundStyle(.primary)
                    .padding(.horizontal, 8)
            }
            .scrollBounceBehavior(.basedOnSize)

            Spacer()

            if let sourceFile {
                HStack(spacing: 4) {
                    Text(sourceFile)
                    if let sourceHeading {
                        Text("\u{00B7}")
                        Text(sourceHeading)
                    }
                }
                .font(.caption2)
                .foregroundStyle(.tertiary)
                .padding(.bottom, 4)
            }
        }
        .frame(maxWidth: .infinity)
        .padding(24)
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
