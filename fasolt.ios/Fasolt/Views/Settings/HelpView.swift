import SwiftUI

struct HelpView: View {
    var body: some View {
        List {
            Section {
                Text("fasolt uses FSRS (Free Spaced Repetition Scheduler) to schedule your reviews. Here's how it works.")
                    .font(.subheadline)
                    .foregroundStyle(FasoltTheme.ink1)
            }

            Section("What is spaced repetition?") {
                Text("Spaced repetition is a study technique where you review material at increasing intervals. Instead of cramming, you see a card right before you're likely to forget it. Each successful recall makes the memory stronger, so the next review can wait longer.")
                    .font(.subheadline)
                    .foregroundStyle(FasoltTheme.ink1)
            }

            Section("The FSRS algorithm") {
                VStack(alignment: .leading, spacing: 12) {
                    Text("FSRS tracks three variables for each card:")
                        .font(.subheadline)
                        .foregroundStyle(FasoltTheme.ink1)

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

            Section("How reviews work") {
                VStack(alignment: .leading, spacing: 8) {
                    Text("When you review a card, you rate how well you recalled it:")
                        .font(.subheadline)
                        .foregroundStyle(FasoltTheme.ink1)

                    ratingItem(
                        rating: "Again",
                        color: FasoltTheme.again,
                        description: "You forgot. Stability resets and the card re-enters the learning phase."
                    )
                    ratingItem(
                        rating: "Hard",
                        color: FasoltTheme.hard,
                        description: "You recalled with significant difficulty. Stability increases slightly, difficulty goes up."
                    )
                    ratingItem(
                        rating: "Good",
                        color: FasoltTheme.good,
                        description: "Normal recall. Stability increases proportionally — the standard path."
                    )
                    ratingItem(
                        rating: "Easy",
                        color: FasoltTheme.easy,
                        description: "Effortless recall. Large stability increase, difficulty decreases."
                    )
                }
            }

            Section("How intervals grow") {
                VStack(alignment: .leading, spacing: 8) {
                    Text("A typical progression for a card rated \"Good\" each time:")
                        .font(.subheadline)
                        .foregroundStyle(FasoltTheme.ink1)

                    HStack(spacing: 4) {
                        ForEach(["1d", "3d", "8d", "21d", "55d", "4mo"], id: \.self) { interval in
                            Text(interval)
                                .font(.caption.monospaced())
                                .foregroundStyle(FasoltTheme.ink0)
                                .padding(.horizontal, 6)
                                .padding(.vertical, 3)
                                .background(FasoltTheme.paper2, in: RoundedRectangle(cornerRadius: 4))
                            if interval != "4mo" {
                                Image(systemName: "arrow.right")
                                    .font(.caption2)
                                    .foregroundStyle(FasoltTheme.ink3)
                            }
                        }
                    }

                    Text("Intervals grow roughly exponentially. \"Easy\" makes them grow faster; \"Again\" resets them.")
                        .font(.subheadline)
                        .foregroundStyle(FasoltTheme.ink1)
                }
            }

            Section("Card states") {
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
        .scrollContentBackground(.hidden)
        .background(FasoltTheme.paper0.ignoresSafeArea())
        .navigationTitle("How it works")
        .navigationBarTitleDisplayMode(.inline)
    }

    private func helpItem(title: String, description: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title)
                .font(.subheadline.weight(.medium))
                .foregroundStyle(FasoltTheme.ink0)
            Text(description)
                .font(.caption)
                .foregroundStyle(FasoltTheme.ink2)
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
                .foregroundStyle(FasoltTheme.ink2)
        }
    }

    private func stateItem(state: String, description: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(state)
                .font(.subheadline.weight(.medium))
                .foregroundStyle(FasoltTheme.ink0)
            Text(description)
                .font(.caption)
                .foregroundStyle(FasoltTheme.ink2)
        }
    }
}

#Preview {
    NavigationStack {
        HelpView()
    }
}
