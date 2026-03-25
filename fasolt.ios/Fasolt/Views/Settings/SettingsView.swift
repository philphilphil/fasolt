import SwiftUI

struct SettingsView: View {
    @Environment(AuthService.self) private var authService
    @State private var viewModel: SettingsViewModel
    @State private var notificationViewModel: NotificationSettingsViewModel
    @State private var showSignOutConfirmation = false

    init(viewModel: SettingsViewModel, notificationViewModel: NotificationSettingsViewModel) {
        _viewModel = State(initialValue: viewModel)
        _notificationViewModel = State(initialValue: notificationViewModel)
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

                        if notificationViewModel.hasDeviceToken {
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
                        }

                        if let error = notificationViewModel.errorMessage {
                            Text(error)
                                .foregroundStyle(.red)
                                .font(.caption)
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
                    authService.signOut()
                }
            } message: {
                Text("You'll need to sign in again to use Fasolt.")
            }
            .task {
                await viewModel.loadUserInfo()
                await notificationViewModel.load()
            }
        }
    }
}
