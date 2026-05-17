import SwiftUI
import UIKit

struct StudyView: View {
    @Environment(\.dismiss) private var dismiss
    @State private var viewModel: StudyViewModel
    private let deckId: String?
    private let mode: StudyMode

    init(viewModel: StudyViewModel, deckId: String? = nil, mode: StudyMode = .normal) {
        _viewModel = State(initialValue: viewModel)
        self.deckId = deckId
        self.mode = mode
    }

    var body: some View {
        Group {
            switch viewModel.state {
            case .idle, .loading:
                loadingView
            case .studying, .flipped:
                cardContent
            case .summary:
                if viewModel.mode == .cram {
                    cramSummary
                } else {
                    StudySummaryView(
                        cardsStudied: viewModel.cardsStudied,
                        ratingsCount: viewModel.ratingsCount,
                        failedRatings: viewModel.failedRatings,
                        skippedCount: viewModel.skippedCount,
                        suspendedCount: viewModel.suspendedCount,
                        onDone: { dismiss() }
                    )
                }
            }
        }
        .toolbar {
            ToolbarItem(placement: .topBarLeading) {
                if viewModel.state == .studying || viewModel.state == .flipped {
                    HStack(spacing: 16) {
                        Button {
                            let generator = UIImpactFeedbackGenerator(style: .light)
                            generator.impactOccurred()
                            Task {
                                await viewModel.suspendCard()
                                if viewModel.state == .summary {
                                    let notification = UINotificationFeedbackGenerator()
                                    notification.notificationOccurred(.success)
                                }
                            }
                        } label: {
                            Image(systemName: "pause.circle")
                                .foregroundStyle(.secondary)
                        }
                        Button {
                            let generator = UIImpactFeedbackGenerator(style: .light)
                            generator.impactOccurred()
                            viewModel.skipCard()
                            if viewModel.state == .summary {
                                let notification = UINotificationFeedbackGenerator()
                                notification.notificationOccurred(.success)
                            }
                        } label: {
                            Text("Skip")
                                .font(.subheadline)
                                .foregroundStyle(.secondary)
                        }
                    }
                }
            }
            ToolbarItem(placement: .topBarTrailing) {
                if viewModel.state != .summary {
                    Button {
                        if (viewModel.cardsStudied > 0 || viewModel.skippedCount > 0) && viewModel.state != .summary {
                            viewModel.state = .summary
                        } else {
                            dismiss()
                        }
                    } label: {
                        Image(systemName: "xmark")
                            .foregroundStyle(.secondary)
                    }
                }
            }
        }
        .task {
            if viewModel.state == .idle {
                await viewModel.startSession(deckId: deckId, mode: mode)
            }
        }
        .offlineBanner()
    }

    // MARK: - Loading

    private var loadingView: some View {
        Group {
            if let error = viewModel.errorMessage {
                ContentUnavailableView {
                    Label("Could not load", systemImage: "wifi.slash")
                } description: {
                    Text(error)
                } actions: {
                    Button("Retry") {
                        Task { await viewModel.startSession(deckId: deckId) }
                    }
                }
            } else {
                ProgressView("Loading cards...")
            }
        }
    }

    // MARK: - Card Content

    private var cardContent: some View {
        VStack(spacing: 0) {
            if viewModel.mode == .cram {
                Text("Custom study — FSRS not adjusted")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity)
                    .padding(.top, 8)
            }

            progressBar
                .padding(.horizontal)
                .padding(.top, 8)

            Spacer()

            if let card = viewModel.currentCard {
                CardView(
                    label: viewModel.isFlipped ? "Answer" : "Question",
                    text: viewModel.isFlipped ? card.back : card.front,
                    sourceFile: card.sourceFile,
                    sourceHeading: viewModel.isFlipped ? card.sourceHeading : nil,
                    svg: viewModel.isFlipped ? card.backSvg : card.frontSvg,
                    cardId: card.id,
                    questionText: viewModel.isFlipped ? card.front : nil
                )
                .padding(.horizontal)
                .rotation3DEffect(
                    .degrees(viewModel.isFlipped ? 180 : 0),
                    axis: (x: 0, y: 1, z: 0),
                    perspective: 0.5
                )
                .scaleEffect(x: viewModel.isFlipped ? -1 : 1)
                .onTapGesture {
                    if !viewModel.isFlipped {
                        flipWithHaptic()
                    }
                }
            }

            Spacer()

            if let ratingError = viewModel.ratingError {
                Text(ratingError)
                    .font(.caption)
                    .foregroundStyle(.orange)
                    .padding(.horizontal)
                    .transition(.opacity)
            }

            if viewModel.isFlipped {
                if viewModel.mode == .cram {
                    nextButton
                        .padding()
                        .transition(.move(edge: .bottom).combined(with: .opacity))
                } else {
                    ratingButtons
                        .padding()
                        .transition(.move(edge: .bottom).combined(with: .opacity))
                }
            } else {
                Button {
                    flipWithHaptic()
                } label: {
                    Text("Show Answer")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.bordered)
                .controlSize(.large)
                .padding()
                .transition(.move(edge: .bottom).combined(with: .opacity))
            }
        }
    }

    private func flipWithHaptic() {
        let generator = UIImpactFeedbackGenerator(style: .light)
        generator.impactOccurred()
        withAnimation(.spring(duration: 0.4)) {
            viewModel.flipCard()
        }
    }

    // MARK: - Progress Bar

    private var progressBar: some View {
        HStack(spacing: 8) {
            Text("\(viewModel.currentIndex + 1) / \(viewModel.totalCards)")
                .font(.caption)
                .foregroundStyle(.secondary)
                .monospacedDigit()

            GeometryReader { geo in
                ZStack(alignment: .leading) {
                    Capsule()
                        .fill(.quaternary)
                    Capsule()
                        .fill(.blue)
                        .frame(width: geo.size.width * viewModel.progress)
                }
            }
            .frame(height: 4)
        }
    }

    // MARK: - Cram Mode

    private var nextButton: some View {
        Button {
            let generator = UIImpactFeedbackGenerator(style: .light)
            generator.impactOccurred()
            viewModel.advance()
            if viewModel.state == .summary {
                let notification = UINotificationFeedbackGenerator()
                notification.notificationOccurred(.success)
            }
        } label: {
            Text("Next")
                .font(.headline)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 4)
        }
        .buttonStyle(.borderedProminent)
        .controlSize(.large)
    }

    private var cramSummary: some View {
        VStack(spacing: 24) {
            Spacer()

            Image(systemName: "checkmark.circle.fill")
                .font(.system(size: 56))
                .foregroundStyle(.green)

            Text("Session Complete")
                .font(.title2.bold())

            Text("\(viewModel.cardsStudied) card\(viewModel.cardsStudied == 1 ? "" : "s") reviewed")
                .font(.body)
                .foregroundStyle(.secondary)

            Spacer()

            Button("Done") {
                dismiss()
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .frame(maxWidth: .infinity)
        }
        .padding()
    }

    // MARK: - Rating Buttons

    private var ratingButtons: some View {
        HStack(spacing: 8) {
            ratingButton("Again", color: .red, rating: "again")
            ratingButton("Hard", color: .orange, rating: "hard")
            ratingButton("Good", color: .green, rating: "good")
            ratingButton("Easy", color: .blue, rating: "easy")
        }
    }

    private func ratingButton(_ label: String, color: Color, rating: String) -> some View {
        Button {
            let generator = UIImpactFeedbackGenerator(style: .light)
            generator.impactOccurred()
            Task {
                await viewModel.rateCard(rating)
                if viewModel.state == .summary {
                    let notification = UINotificationFeedbackGenerator()
                    notification.notificationOccurred(.success)
                }
            }
        } label: {
            Text(label)
                .font(.subheadline.weight(.medium))
                .frame(maxWidth: .infinity)
                .padding(.vertical, 12)
        }
        .buttonStyle(.bordered)
        .tint(color)
        .disabled(viewModel.isRating)
    }
}
