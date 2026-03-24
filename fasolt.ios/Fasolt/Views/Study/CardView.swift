import SwiftUI

struct CardView: View {
    let label: String
    let text: String
    let sourceFile: String?
    let sourceHeading: String?

    var body: some View {
        VStack(spacing: 0) {
            Spacer()

            Text(label)
                .font(.caption2)
                .textCase(.uppercase)
                .tracking(1)
                .foregroundStyle(.secondary)
                .padding(.bottom, 12)

            Text(text)
                .font(.title3)
                .multilineTextAlignment(.center)
                .foregroundStyle(.primary)
                .padding(.horizontal, 8)

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
