import SwiftUI

struct HelpView: View {
    var body: some View {
        List {
            Section {
                Text("fasolt uses FSRS (Free Spaced Repetition Scheduler) to schedule your reviews. Here's how it works.")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }

            Section("What is Spaced Repetition?") {
                Text("Spaced repetition is a study technique where you review material at increasing intervals. Instead of cramming, you see a card right before you're likely to forget it. Each successful recall makes the memory stronger, so the next review can wait longer.")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }

            Section("The FSRS Algorithm") {
                VStack(alignment: .leading, spacing: 12) {
                    Text("FSRS tracks three variables for each card:")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)

                    VStack(alignment: .leading, spacing: 8) {
                        helpItem(
                            title: "Stability (S)",
                            description: "How long the memory lasts. Higher stability means longer intervals between reviews."
                        )
                        helpItem(
                            title: "Difficulty (D)",
                            description: "How inherently hard the card is for you. Updated with each review based on your rating."
                        )
                        helpItem(
                            title: "Retrievability (R)",
                            description: "The probability you can recall the card right now. Decays over time — when it drops below the target retention, the card becomes due."
                        )
                    }
                }
            }

            Section("How Reviews Work") {
                VStack(alignment: .leading, spacing: 8) {
                    Text("When you review a card, you rate how well you recalled it:")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)

                    ratingItem(
                        rating: "Again",
                        color: .red,
                        description: "You forgot. Stability resets and the card re-enters the learning phase."
                    )
                    ratingItem(
                        rating: "Hard",
                        color: .orange,
                        description: "You recalled with significant difficulty. Stability increases slightly, difficulty goes up."
                    )
                    ratingItem(
                        rating: "Good",
                        color: .green,
                        description: "Normal recall. Stability increases proportionally — the standard path."
                    )
                    ratingItem(
                        rating: "Easy",
                        color: .blue,
                        description: "Effortless recall. Large stability increase, difficulty decreases."
                    )
                }
            }

            Section("How Intervals Grow") {
                VStack(alignment: .leading, spacing: 8) {
                    Text("A typical progression for a card rated \"Good\" each time:")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)

                    HStack(spacing: 4) {
                        ForEach(["1d", "3d", "8d", "21d", "55d", "4mo"], id: \.self) { interval in
                            Text(interval)
                                .font(.caption.monospaced())
                                .padding(.horizontal, 6)
                                .padding(.vertical, 3)
                                .background(.quaternary, in: RoundedRectangle(cornerRadius: 4))
                            if interval != "4mo" {
                                Image(systemName: "arrow.right")
                                    .font(.caption2)
                                    .foregroundStyle(.tertiary)
                            }
                        }
                    }

                    Text("Intervals grow roughly exponentially. \"Easy\" makes them grow faster; \"Again\" resets them.")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }
            }

            Section("Card States") {
                VStack(alignment: .leading, spacing: 8) {
                    stateItem(
                        state: "New",
                        description: "Never reviewed. Will enter the learning phase on first review."
                    )
                    stateItem(
                        state: "Learning",
                        description: "Recently introduced or reset. Reviewed at short intervals until stable."
                    )
                    stateItem(
                        state: "Review",
                        description: "Graduated from learning. Intervals grow with each successful recall."
                    )
                    stateItem(
                        state: "Relearning",
                        description: "Previously known but forgotten (rated \"Again\"). Short intervals until re-stabilized."
                    )
                    stateItem(
                        state: "Suspended",
                        description: "Temporarily excluded from review. Can be unsuspended at any time."
                    )
                }
            }
        }
        .navigationTitle("How It Works")
        .navigationBarTitleDisplayMode(.inline)
    }

    private func helpItem(title: String, description: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title)
                .font(.subheadline.weight(.medium))
            Text(description)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
    }

    private func ratingItem(rating: String, color: Color, description: String) -> some View {
        HStack(alignment: .top, spacing: 8) {
            Text(rating)
                .font(.caption.weight(.semibold))
                .foregroundStyle(color)
                .frame(width: 44, alignment: .leading)
            Text(description)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
    }

    private func stateItem(state: String, description: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(state)
                .font(.subheadline.weight(.medium))
            Text(description)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
    }
}

#Preview {
    NavigationStack {
        HelpView()
    }
}
