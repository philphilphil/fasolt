import SwiftUI

struct StudySummaryView: View {
    let cardsStudied: Int
    let ratingsCount: [String: Int]
    var failedRatings: Int = 0
    var skippedCount: Int = 0
    var suspendedCount: Int = 0
    let onDone: () -> Void

    var body: some View {
        VStack(spacing: 24) {
            Spacer()

            Image(systemName: "checkmark.circle.fill")
                .font(.system(size: 56))
                .foregroundStyle(FasoltTheme.good)

            Text("Session complete")
                .font(.system(size: 22, weight: .semibold))
                .foregroundStyle(FasoltTheme.ink0)

            VStack(spacing: 12) {
                HStack {
                    Text("Cards studied")
                        .font(.system(size: 15))
                        .foregroundStyle(FasoltTheme.ink2)
                    Spacer()
                    Text("\(cardsStudied)")
                        .font(.system(size: 15, weight: .semibold))
                        .monospacedDigit()
                        .foregroundStyle(FasoltTheme.ink0)
                }

                Rectangle()
                    .fill(FasoltTheme.rule2)
                    .frame(height: FasoltTheme.hairline)

                ratingRow("Again", count: ratingsCount["again"] ?? 0, color: FasoltTheme.again)
                ratingRow("Hard", count: ratingsCount["hard"] ?? 0, color: FasoltTheme.hard)
                ratingRow("Good", count: ratingsCount["good"] ?? 0, color: FasoltTheme.good)
                ratingRow("Easy", count: ratingsCount["easy"] ?? 0, color: FasoltTheme.easy)

                if skippedCount > 0 {
                    ratingRow("Skipped", count: skippedCount, color: FasoltTheme.ink3)
                }

                if suspendedCount > 0 {
                    ratingRow("Suspended", count: suspendedCount, color: FasoltTheme.ink3)
                }
            }
            .padding(16)
            .paperCard()

            if failedRatings > 0 {
                Label(
                    "\(failedRatings) rating\(failedRatings == 1 ? "" : "s") may not have been saved. They'll sync when you're back online.",
                    systemImage: "exclamationmark.triangle"
                )
                .font(.system(size: 12))
                .foregroundStyle(FasoltTheme.ink2)
                .padding(.horizontal)
            }

            Spacer()

            Button("Done") {
                onDone()
            }
            .buttonStyle(AccentButtonStyle())
        }
        .padding()
        .background(FasoltTheme.paper0.ignoresSafeArea())
    }

    private func ratingRow(_ label: String, count: Int, color: Color) -> some View {
        HStack {
            Circle()
                .fill(color)
                .frame(width: 8, height: 8)
            Text(label)
                .font(.system(size: 15))
                .foregroundStyle(FasoltTheme.ink2)
            Spacer()
            Text("\(count)")
                .font(.system(size: 15, weight: .medium))
                .monospacedDigit()
                .foregroundStyle(FasoltTheme.ink0)
        }
    }
}

#Preview {
    StudySummaryView(
        cardsStudied: 23,
        ratingsCount: ["again": 3, "hard": 5, "good": 12, "easy": 3],
        onDone: {}
    )
}
