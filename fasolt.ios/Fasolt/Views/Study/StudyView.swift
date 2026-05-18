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
                // Cram sessions dismiss directly via .onChange below — never reaches here.
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
        .background(FasoltTheme.paper0.ignoresSafeArea())
        .onChange(of: viewModel.state) { _, newState in
            if newState == .summary && viewModel.mode == .cram {
                UINotificationFeedbackGenerator().notificationOccurred(.success)
                dismiss()
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
                                .foregroundStyle(FasoltTheme.ink1)
                        }
                        if viewModel.mode != .cram {
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
                                    .font(.system(size: 15, weight: .medium))
                                    .foregroundStyle(FasoltTheme.ink1)
                            }
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
                            .foregroundStyle(FasoltTheme.ink1)
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
                    .font(.system(size: 12))
                    .foregroundStyle(FasoltTheme.ink2)
                    .frame(maxWidth: .infinity)
                    .padding(.top, 6)
            }

            progressBar
                .padding(.horizontal, 20)
                .padding(.top, 8)
                .padding(.bottom, 6)

            if let card = viewModel.currentCard {
                CardView(
                    label: viewModel.isFlipped ? "Answer" : "Question",
                    text: viewModel.isFlipped ? card.back : card.front,
                    sourceFile: card.sourceFile,
                    sourceHeading: viewModel.isFlipped ? card.sourceHeading : nil,
                    svg: viewModel.isFlipped ? card.backSvg : card.frontSvg,
                    cardId: card.id,
                    questionText: viewModel.isFlipped ? card.front : nil,
                    showAnswer: viewModel.isFlipped
                )
                .padding(.horizontal, 16)
                .padding(.top, 8)
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

            if let ratingError = viewModel.ratingError {
                Text(ratingError)
                    .font(.system(size: 12))
                    .foregroundStyle(FasoltTheme.hard)
                    .padding(.horizontal)
                    .transition(.opacity)
            }

            if viewModel.isFlipped {
                if viewModel.mode == .cram {
                    nextButton
                        .padding(.horizontal, 16)
                        .padding(.top, 12)
                        .padding(.bottom, 16)
                        .transition(.move(edge: .bottom).combined(with: .opacity))
                } else {
                    ratingButtons
                        .padding(.horizontal, 16)
                        .padding(.top, 12)
                        .padding(.bottom, 16)
                        .transition(.move(edge: .bottom).combined(with: .opacity))
                }
            } else {
                Button {
                    flipWithHaptic()
                } label: {
                    Text("Show answer")
                }
                .buttonStyle(AccentButtonStyle(height: 54, radius: 16, fontSize: 17))
                .padding(.horizontal, 16)
                .padding(.top, 12)
                .padding(.bottom, 16)
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
        HStack(spacing: 10) {
            Text("\(viewModel.currentIndex + 1) / \(viewModel.totalCards)")
                .font(.system(size: 13, weight: .medium))
                .monospacedDigit()
                .foregroundStyle(FasoltTheme.ink2)

            GeometryReader { geo in
                ZStack(alignment: .leading) {
                    Capsule().fill(FasoltTheme.rule2)
                    Capsule()
                        .fill(FasoltTheme.accent)
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
        } label: {
            Text("Next")
        }
        .buttonStyle(AccentButtonStyle(height: 54, radius: 16, fontSize: 17))
    }

    // MARK: - Rating Buttons

    private var ratingButtons: some View {
        HStack(spacing: 8) {
            ratingTile("Again", color: FasoltTheme.again, rating: "again")
            ratingTile("Hard",  color: FasoltTheme.hard,  rating: "hard")
            ratingTile("Good",  color: FasoltTheme.good,  rating: "good")
            ratingTile("Easy",  color: FasoltTheme.easy,  rating: "easy")
        }
    }

    private func ratingTile(_ label: String, color: Color, rating: String) -> some View {
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
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(color)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 14)
                .padding(.horizontal, 6)
                .background(FasoltTheme.paper1)
                .overlay(
                    RoundedRectangle(cornerRadius: 12, style: .continuous)
                        .strokeBorder(FasoltTheme.rule2, lineWidth: FasoltTheme.hairline)
                )
                .overlay(alignment: .bottom) {
                    Rectangle()
                        .fill(color)
                        .frame(height: 2.5)
                        .clipShape(RoundedRectangle(cornerRadius: 1.5))
                }
                .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
        }
        .buttonStyle(.plain)
        .disabled(viewModel.isRating)
    }
}
