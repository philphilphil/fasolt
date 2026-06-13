import SwiftUI

struct SnapshotsView: View {
    @State var viewModel: SnapshotViewModel
    @State private var showSuccess = false
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 14) {
                    infoCard
                    createCard
                    historySection
                }
                .padding(.horizontal, FasoltTheme.pagePadding)
                .padding(.top, 8)
                .padding(.bottom, 24)
            }
            .background(FasoltTheme.paper0.ignoresSafeArea())
            .scrollContentBackground(.hidden)
            .navigationTitle("Snapshots")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Done") { dismiss() }
                }
            }
            .task { await viewModel.loadSnapshots() }
            .alert("Snapshot", isPresented: $showSuccess) {
                Button("OK", role: .cancel) {}
            } message: {
                if let count = viewModel.createSuccessCount, count > 0 {
                    Text("Created snapshots for \(count) deck(s).")
                } else {
                    Text("All decks unchanged — no snapshots created.")
                }
            }
        }
    }

    // MARK: - Info

    private var infoCard: some View {
        HStack(alignment: .firstTextBaseline, spacing: 8) {
            Image(systemName: "info.circle")
                .font(.system(size: 14))
                .foregroundStyle(FasoltTheme.ink2)
            (
                Text("Snapshots back up every card's content. The last 10 snapshots per deck are kept automatically. Restoring only reverts card content — your study progress is never affected. To restore, visit ")
                + Text("[fasolt.app](https://fasolt.app)")
                    .foregroundColor(FasoltTheme.accentText)
                + Text(".")
            )
            .font(.system(size: 13))
            .foregroundStyle(FasoltTheme.ink2)
            .tint(FasoltTheme.accentText)
        }
        .padding(16)
        .paperCard()
    }

    // MARK: - Create

    private var createCard: some View {
        VStack(alignment: .leading, spacing: 0) {
            Button {
                Task {
                    await viewModel.createSnapshot()
                    if viewModel.createSuccessCount != nil {
                        showSuccess = true
                    }
                }
            } label: {
                HStack(spacing: 12) {
                    Image(systemName: "camera.viewfinder")
                        .font(.system(size: 16, weight: .semibold))
                        .foregroundStyle(FasoltTheme.accentText)
                    Text("Create snapshot")
                        .font(.system(size: 16, weight: .medium))
                        .foregroundStyle(FasoltTheme.accentText)
                    Spacer(minLength: 8)
                    if viewModel.isCreating {
                        ProgressView()
                    }
                }
                .padding(.horizontal, 16)
                .padding(.vertical, 14)
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .disabled(viewModel.isCreating)

            if let error = viewModel.errorMessage {
                Rectangle()
                    .fill(FasoltTheme.rule2)
                    .frame(height: FasoltTheme.hairline)
                    .padding(.leading, 16)
                Text(error)
                    .font(.system(size: 12))
                    .foregroundStyle(FasoltTheme.again)
                    .padding(.horizontal, 16)
                    .padding(.vertical, 10)
            }
        }
        .paperCard()
    }

    // MARK: - History

    private var historySection: some View {
        VStack(alignment: .leading, spacing: 8) {
            CapsLabel(text: "History", size: 12)
                .padding(.horizontal, 16)
                .padding(.top, 8)

            VStack(spacing: 0) {
                if viewModel.isLoading && viewModel.snapshots.isEmpty {
                    HStack {
                        Text("Loading…")
                            .font(.system(size: 14))
                            .foregroundStyle(FasoltTheme.ink2)
                        Spacer()
                        ProgressView()
                    }
                    .padding(.horizontal, 16)
                    .padding(.vertical, 14)
                } else if viewModel.snapshots.isEmpty {
                    Text("No snapshots yet.")
                        .font(.system(size: 14))
                        .foregroundStyle(FasoltTheme.ink2)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal, 16)
                        .padding(.vertical, 14)
                } else {
                    ForEach(Array(viewModel.snapshots.enumerated()), id: \.element.id) { index, snapshot in
                        snapshotRow(snapshot, isLast: index == viewModel.snapshots.count - 1)
                    }
                }
            }
            .paperCard()
        }
    }

    private func snapshotRow(_ snapshot: DeckSnapshotDTO, isLast: Bool) -> some View {
        let deckName = snapshot.deckName ?? "Unknown deck"
        return HStack(spacing: 12) {
            DeckTag(color: FasoltTheme.deckColor(for: deckName), size: 10)
            VStack(alignment: .leading, spacing: 2) {
                Text(deckName)
                    .font(.system(size: 16, weight: .medium))
                    .foregroundStyle(FasoltTheme.ink0)
                    .lineLimit(1)
                Text(formatSnapshotDate(snapshot.createdAt))
                    .font(.system(size: 12, design: .monospaced))
                    .foregroundStyle(FasoltTheme.ink2)
            }
            Spacer(minLength: 8)
            Text("\(snapshot.cardCount) cards")
                .font(.system(size: 13))
                .monospacedDigit()
                .foregroundStyle(FasoltTheme.ink2)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
        .overlay(alignment: .bottom) {
            if !isLast {
                Rectangle()
                    .fill(FasoltTheme.rule2)
                    .frame(height: FasoltTheme.hairline)
                    .padding(.leading, 38)
            }
        }
    }

    private func formatSnapshotDate(_ isoString: String) -> String {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        guard let date = formatter.date(from: isoString) else { return isoString }
        let display = DateFormatter()
        display.dateStyle = .medium
        display.timeStyle = .short
        return display.string(from: date)
    }
}
