import SwiftUI
import UIKit

struct StudyView: View {
    @Environment(\.dismiss) private var dismiss
    @State private var viewModel: StudyViewModel
    @State private var showExitConfirmation = false
    private let deckId: String?

    init(viewModel: StudyViewModel, deckId: String? = nil) {
        _viewModel = State(initialValue: viewModel)
        self.deckId = deckId
    }

    var body: some View {
        Group {
            switch viewModel.state {
            case .idle, .loading:
                loadingView
            case .studying, .flipped:
                cardContent
            case .summary:
                StudySummaryView(
                    cardsStudied: viewModel.cardsStudied,
                    ratingsCount: viewModel.ratingsCount,
                    onDone: { dismiss() }
                )
            }
        }
        .navigationBarBackButtonHidden(true)
        .toolbar {
            ToolbarItem(placement: .topBarLeading) {
                if viewModel.state != .summary {
                    Button {
                        if viewModel.cardsStudied > 0 && viewModel.state != .summary {
                            showExitConfirmation = true
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
        .alert("End Session?", isPresented: $showExitConfirmation) {
            Button("Keep Studying", role: .cancel) {}
            Button("End", role: .destructive) { dismiss() }
        } message: {
            Text("You've studied \(viewModel.cardsStudied) of \(viewModel.totalCards) cards.")
        }
        .task {
            if viewModel.state == .idle {
                await viewModel.startSession(deckId: deckId)
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
            progressBar
                .padding(.horizontal)
                .padding(.top, 8)

            Spacer()

            if let card = viewModel.currentCard {
                CardView(
                    label: viewModel.isFlipped ? "Answer" : "Question",
                    text: viewModel.isFlipped ? card.back : card.front,
                    sourceFile: card.sourceFile,
                    sourceHeading: viewModel.isFlipped ? card.sourceHeading : nil
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

            if viewModel.isFlipped {
                ratingButtons
                    .padding()
                    .transition(.move(edge: .bottom).combined(with: .opacity))
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
    }
}
