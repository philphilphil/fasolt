import SwiftUI
import Textual

struct CardView: View {
    let label: String
    let text: String
    let sourceFile: String?
    var svg: String? = nil
    var cardId: String? = nil
    var questionText: String? = nil
    var showAnswer: Bool = false

    @State private var idCopied = false

    var body: some View {
        ZStack(alignment: .top) {
            RoundedRectangle(cornerRadius: 20, style: .continuous)
                .fill(FasoltTheme.paper1)
                .overlay(
                    RoundedRectangle(cornerRadius: 20, style: .continuous)
                        .strokeBorder(FasoltTheme.rule2, lineWidth: FasoltTheme.hairline)
                )
                .shadow(color: .black.opacity(0.04), radius: 12, x: 0, y: 4)

            VStack(spacing: 0) {
                AccentStripe(horizontalInset: 24)
                    .opacity(showAnswer ? 1 : 0.45)

                VStack(spacing: 0) {
                    // Header: label + source file
                    HStack(alignment: .firstTextBaseline) {
                        CapsLabel(text: label, color: FasoltTheme.accent, size: 11)
                        Spacer(minLength: 8)
                        if let sourceFile {
                            HStack(spacing: 4) {
                                Image(systemName: "doc.text")
                                    .font(.system(size: 10))
                                Text(sourceFile)
                                    .font(.system(size: 11, design: .monospaced))
                            }
                            .foregroundStyle(FasoltTheme.ink2)
                            .lineLimit(1)
                        }
                    }
                    .padding(.horizontal, 22)
                    .padding(.top, 20)
                    .padding(.bottom, 12)

                    // Question echoed on the back
                    if let questionText {
                        VStack(alignment: .leading, spacing: 4) {
                            StructuredText(markdown: questionText)
                                .font(.system(size: 13))
                                .foregroundStyle(FasoltTheme.ink2)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal, 22)
                        .padding(.bottom, 6)
                    }

                    if let svg {
                        SvgView(svg: svg)
                            .frame(maxWidth: .infinity)
                            .frame(height: 220)
                            .padding(.horizontal, 16)
                            .padding(.bottom, 8)
                    }

                    ScrollView {
                        StructuredText(markdown: text)
                            .font(.system(size: showAnswer ? 16 : 22, weight: showAnswer ? .regular : .semibold))
                            .foregroundStyle(FasoltTheme.ink0)
                            .frame(maxWidth: .infinity, alignment: showAnswer ? .leading : .center)
                            .padding(.horizontal, 22)
                            .padding(.vertical, showAnswer ? 4 : 16)
                            .multilineTextAlignment(showAnswer ? .leading : .center)
                    }
                    .scrollBounceBehavior(.basedOnSize)
                    .frame(maxHeight: .infinity)

                    // Footer
                    HStack(alignment: .firstTextBaseline) {
                        if !showAnswer {
                            Text("tap to reveal")
                                .font(.system(size: 11))
                                .foregroundStyle(FasoltTheme.ink2)
                                .frame(maxWidth: .infinity, alignment: .trailing)
                        }

                        Spacer(minLength: 6)

                        if let cardId {
                            Button {
                                UIPasteboard.general.string = cardId
                                idCopied = true
                                DispatchQueue.main.asyncAfter(deadline: .now() + 2) {
                                    idCopied = false
                                }
                            } label: {
                                Text(idCopied ? "Copied!" : String(cardId.prefix(8)))
                                    .font(.system(size: 11, design: .monospaced))
                                    .foregroundStyle(FasoltTheme.ink3)
                            }
                            .buttonStyle(.plain)
                        }
                    }
                    .padding(.horizontal, 22)
                    .padding(.bottom, 14)
                    .padding(.top, 8)
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

#Preview("Question") {
    CardView(
        label: "Question",
        text: "What organelle is responsible for producing ATP?",
        sourceFile: "biology-101.md"
    )
    .padding()
    .background(FasoltTheme.paper0)
}

#Preview("With SVG") {
    CardView(
        label: "Question",
        text: "What shape is this?",
        sourceFile: nil,
        svg: "<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"50\" cy=\"50\" r=\"40\" fill=\"blue\"/></svg>"
    )
    .padding()
    .background(FasoltTheme.paper0)
}
