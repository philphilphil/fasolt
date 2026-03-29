import SwiftUI

enum SettingsSegment: String, CaseIterable {
    case settings = "Settings"
    case snapshots = "Snapshots"
}

struct SettingsView: View {
    @Environment(AuthService.self) private var authService
    @State private var viewModel: SettingsViewModel
    @State private var notificationViewModel: NotificationSettingsViewModel
    @State private var schedulingViewModel: SchedulingSettingsViewModel
    @State private var snapshotViewModel: SnapshotViewModel
    @State private var selectedSegment: SettingsSegment = .settings
    @State private var showSignOutConfirmation = false
    @State private var showSnapshotSuccess = false

    init(viewModel: SettingsViewModel, notificationViewModel: NotificationSettingsViewModel, schedulingViewModel: SchedulingSettingsViewModel, snapshotViewModel: SnapshotViewModel) {
        _viewModel = State(initialValue: viewModel)
        _notificationViewModel = State(initialValue: notificationViewModel)
        _schedulingViewModel = State(initialValue: schedulingViewModel)
        _snapshotViewModel = State(initialValue: snapshotViewModel)
    }

    var body: some View {
        NavigationStack {
            VStack(spacing: 0) {
                Picker("View", selection: $selectedSegment) {
                    ForEach(SettingsSegment.allCases, id: \.self) { segment in
                        Text(segment.rawValue).tag(segment)
                    }
                }
                .pickerStyle(.segmented)
                .padding(.horizontal)
                .padding(.vertical, 8)

                switch selectedSegment {
                case .settings:
                    settingsContent
                case .snapshots:
                    snapshotsContent
                }
            }
            .navigationTitle("Settings")
            .offlineBanner()
            .alert("Sign Out?", isPresented: $showSignOutConfirmation) {
                Button("Cancel", role: .cancel) {}
                Button("Sign Out", role: .destructive) {
                    Task { await authService.signOut() }
                }
            } message: {
                Text("You'll need to sign in again to use Fasolt.")
            }
            .alert("Snapshot Created", isPresented: $showSnapshotSuccess) {
                Button("OK", role: .cancel) {}
            } message: {
                Text("Created snapshots for \(snapshotViewModel.createSuccessCount ?? 0) deck(s).")
            }
            .task {
                await viewModel.loadUserInfo()
                await notificationViewModel.load()
                await schedulingViewModel.load()
                await snapshotViewModel.loadSnapshots()
            }
        }
    }

    // MARK: - Settings Tab

    private var settingsContent: some View {
        List {
            Section("Account") {
                if viewModel.isLoading {
                    HStack {
                        Text("Loading...")
                            .foregroundStyle(.secondary)
                        Spacer()
                        ProgressView()
                    }
                } else if let error = viewModel.errorMessage {
                    HStack {
                        Label(error, systemImage: "exclamationmark.triangle")
                            .foregroundStyle(.secondary)
                        Spacer()
                        Button("Retry") {
                            Task { await viewModel.loadUserInfo() }
                        }
                        .font(.subheadline)
                    }
                } else {
                    if let email = viewModel.email {
                        LabeledContent("Email", value: email)
                    }
                    if let serverURL = viewModel.serverURL {
                        LabeledContent("Server", value: serverURL)
                            .lineLimit(1)
                    }
                }
            }

            McpSetupSection(serverURL: authService.serverURL)

            Section("Notifications") {
                if notificationViewModel.isLoading {
                    HStack {
                        Text("Loading...")
                            .foregroundStyle(.secondary)
                        Spacer()
                        ProgressView()
                    }
                } else {
                    LabeledContent("Permission", value: notificationViewModel.permissionLabel)
                        .onTapGesture {
                            if notificationViewModel.isPermissionDenied {
                                if let url = URL(string: UIApplication.openSettingsURLString) {
                                    UIApplication.shared.open(url)
                                }
                            }
                        }

                    Picker("Check interval", selection: Binding(
                            get: { notificationViewModel.intervalHours },
                            set: { newValue in
                                Task { await notificationViewModel.updateInterval(newValue) }
                            }
                        )) {
                            ForEach(NotificationSettingsViewModel.allowedIntervals, id: \.self) { hours in
                                Text("Every \(hours)h").tag(hours)
                            }
                        }

                    Label("How often to check for due cards and send a notification.", systemImage: "info.circle")
                        .font(.caption)
                        .foregroundStyle(.secondary)

                    if let error = notificationViewModel.errorMessage {
                        Text(error)
                            .foregroundStyle(.red)
                            .font(.caption)
                    }
                }
            }

            Section("Scheduling") {
                if schedulingViewModel.isLoading {
                    HStack {
                        Text("Loading...")
                            .foregroundStyle(.secondary)
                        Spacer()
                        ProgressView()
                    }
                } else {
                    if let success = schedulingViewModel.successMessage {
                        Text(success)
                            .foregroundStyle(.green)
                            .font(.caption)
                    }
                    if let error = schedulingViewModel.errorMessage {
                        Text(error)
                            .foregroundStyle(.red)
                            .font(.caption)
                    }

                    VStack(alignment: .leading, spacing: 4) {
                        HStack {
                            Text("Desired retention")
                            Spacer()
                            Text(String(format: "%.0f%%", schedulingViewModel.desiredRetention * 100))
                                .foregroundStyle(.secondary)
                        }
                        Slider(
                            value: $schedulingViewModel.desiredRetention,
                            in: 0.70...0.97,
                            step: 0.01
                        )
                        Text("How likely you want to remember a card when it comes up for review. Higher values mean more frequent reviews but stronger recall. Lower values mean fewer reviews but more forgetting. Changes apply to future reviews only.")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }

                    VStack(alignment: .leading, spacing: 4) {
                        HStack {
                            Text("Maximum interval")
                            Spacer()
                            Text("\(schedulingViewModel.maximumInterval) days")
                                .foregroundStyle(.secondary)
                        }
                        TextField("Days", value: $schedulingViewModel.maximumInterval, format: .number)
                            .keyboardType(.numberPad)
                            .textFieldStyle(.roundedBorder)
                        Text("The longest gap between reviews, in days. 365 means every card is seen at least once a year. Default is 36500 (≈ 100 years).")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }

                    Button("Save") {
                        Task { await schedulingViewModel.save() }
                    }
                }
            }

            Section {
                Button("Sign Out", role: .destructive) {
                    showSignOutConfirmation = true
                }
            }

            Section("About") {
                LabeledContent("Version", value: viewModel.appVersion)
            }
        }
    }

    // MARK: - Snapshots Tab

    private var snapshotsContent: some View {
        List {
            Section {
                Label("Snapshots back up every card's content. The last 10 snapshots per deck are kept automatically. Restoring only reverts card content — your study progress is never affected. To restore, visit fasolt.app.", systemImage: "info.circle")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }

            Section {
                Button {
                    Task {
                        await snapshotViewModel.createSnapshot()
                        if snapshotViewModel.createSuccessCount != nil {
                            showSnapshotSuccess = true
                        }
                    }
                } label: {
                    HStack {
                        Text("Create Snapshot")
                        Spacer()
                        if snapshotViewModel.isCreating {
                            ProgressView()
                        }
                    }
                }
                .disabled(snapshotViewModel.isCreating)

                if let error = snapshotViewModel.errorMessage {
                    Text(error)
                        .foregroundStyle(.red)
                        .font(.caption)
                }
            }

            Section("History") {
                if snapshotViewModel.isLoading && snapshotViewModel.snapshots.isEmpty {
                    HStack {
                        Text("Loading...")
                            .foregroundStyle(.secondary)
                        Spacer()
                        ProgressView()
                    }
                } else if snapshotViewModel.snapshots.isEmpty {
                    Text("No snapshots yet.")
                        .foregroundStyle(.secondary)
                } else {
                    ForEach(snapshotViewModel.snapshots) { snapshot in
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
