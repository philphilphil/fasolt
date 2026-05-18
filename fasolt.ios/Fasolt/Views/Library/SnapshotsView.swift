import SwiftUI

struct SnapshotsView: View {
    @State var viewModel: SnapshotViewModel
    @State private var showSuccess = false
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            List {
                Section {
                    HStack(alignment: .firstTextBaseline, spacing: 8) {
                        Image(systemName: "info.circle")
                            .foregroundStyle(.secondary)
                        (
                            Text("Snapshots back up every card's content. The last 10 snapshots per deck are kept automatically. Restoring only reverts card content — your study progress is never affected. To restore, visit ")
                            + Text("[fasolt.app](https://fasolt.app)")
                                .foregroundColor(FasoltTheme.accent)
                            + Text(".")
                        )
                        .tint(FasoltTheme.accent)
                    }
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                }

                Section {
                    Button {
                        Task {
                            await viewModel.createSnapshot()
                            if viewModel.createSuccessCount != nil {
                                showSuccess = true
                            }
                        }
                    } label: {
                        HStack {
                            Text("Create Snapshot")
                            Spacer()
                            if viewModel.isCreating {
                                ProgressView()
                            }
                        }
                    }
                    .disabled(viewModel.isCreating)

                    if let error = viewModel.errorMessage {
                        Text(error)
                            .foregroundStyle(.red)
                            .font(.caption)
                    }
                }

                Section("History") {
                    if viewModel.isLoading && viewModel.snapshots.isEmpty {
                        HStack {
                            Text("Loading...")
                                .foregroundStyle(.secondary)
                            Spacer()
                            ProgressView()
                        }
                    } else if viewModel.snapshots.isEmpty {
                        Text("No snapshots yet.")
                            .foregroundStyle(.secondary)
                    } else {
                        ForEach(viewModel.snapshots) { snapshot in
                            HStack {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text(snapshot.deckName ?? "Unknown deck")
                                        .font(.body)
                                    Text(formatSnapshotDate(snapshot.createdAt))
                                        .font(.caption)
                                        .foregroundStyle(.secondary)
                                }
                                Spacer()
                                Text("\(snapshot.cardCount) cards")
                                    .font(.subheadline)
                                    .foregroundStyle(.secondary)
                            }
                        }
                    }
                }
            }
            .scrollContentBackground(.hidden)
            .background(FasoltTheme.paper0.ignoresSafeArea())
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
