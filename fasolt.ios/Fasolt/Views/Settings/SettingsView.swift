import SwiftUI

struct SettingsView: View {
    @Environment(AuthService.self) private var authService
    @State private var viewModel: SettingsViewModel
    @State private var notificationViewModel: NotificationSettingsViewModel
    @State private var schedulingViewModel: SchedulingSettingsViewModel
    @State private var showSignOutConfirmation = false

    init(viewModel: SettingsViewModel, notificationViewModel: NotificationSettingsViewModel, schedulingViewModel: SchedulingSettingsViewModel) {
        _viewModel = State(initialValue: viewModel)
        _notificationViewModel = State(initialValue: notificationViewModel)
        _schedulingViewModel = State(initialValue: schedulingViewModel)
    }

    var body: some View {
        NavigationStack {
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
            .task {
                await viewModel.loadUserInfo()
                await notificationViewModel.load()
                await schedulingViewModel.load()
            }
        }
    }
}
