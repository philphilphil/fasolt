import SwiftUI

struct StudySummaryView: View {
    let cardsStudied: Int
    let ratingsCount: [String: Int]
    var failedRatings: Int = 0
    let onDone: () -> Void

    var body: some View {
        VStack(spacing: 24) {
            Spacer()

            Image(systemName: "checkmark.circle.fill")
                .font(.system(size: 56))
                .foregroundStyle(.green)

            Text("Session Complete")
                .font(.title2.bold())

            VStack(spacing: 12) {
                HStack {
                    Text("Cards studied")
                        .foregroundStyle(.secondary)
                    Spacer()
                    Text("\(cardsStudied)")
                        .fontWeight(.semibold)
                }

                Divider()

                ratingRow("Again", count: ratingsCount["again"] ?? 0, color: .red)
                ratingRow("Hard", count: ratingsCount["hard"] ?? 0, color: .orange)
                ratingRow("Good", count: ratingsCount["good"] ?? 0, color: .green)
                ratingRow("Easy", count: ratingsCount["easy"] ?? 0, color: .blue)
            }
            .padding()
            .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 12))

            if failedRatings > 0 {
                Label(
                    "\(failedRatings) rating\(failedRatings == 1 ? "" : "s") may not have been saved. They'll sync when you're back online.",
                    systemImage: "exclamationmark.triangle"
                )
                .font(.caption)
                .foregroundStyle(.orange)
                .padding(.horizontal)
            }

            Spacer()

            Button("Done") {
                onDone()
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .frame(maxWidth: .infinity)
        }
        .padding()
    }

    private func ratingRow(_ label: String, count: Int, color: Color) -> some View {
        HStack {
            Circle()
                .fill(color)
                .frame(width: 8, height: 8)
            Text(label)
                .foregroundStyle(.secondary)
            Spacer()
            Text("\(count)")
                .fontWeight(.medium)
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
